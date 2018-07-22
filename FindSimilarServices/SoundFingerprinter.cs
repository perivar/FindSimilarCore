using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SoundFingerprinting;
using SoundFingerprinting.Audio;
using SoundFingerprinting.Builder;
using SoundFingerprinting.Configuration;
using SoundFingerprinting.DAO.Data;
using SoundFingerprinting.InMemory;
using SoundFingerprinting.Query;
using CommonUtils;
using CommonUtils.Audio;
using SoundFingerprinting.Strides;
using FindSimilarServices.Audio;
using SoundFingerprinting.FFT;
using SoundFingerprinting.LSH;
using SoundFingerprinting.MinHash;
using SoundFingerprinting.Wavelets;
using SoundFingerprinting.Utils;
using SoundFingerprinting.Math;
using Serilog;
using CommonUtils.FFT;

namespace FindSimilarServices
{
    // These Verbosity levels roughtly maps to Serilog levels
    public enum Verbosity : int
    {
        /// <summary>
        /// Serilog: Fatal, The most critical level, Fatal events demand immediate attention.
        /// </summary>
        Silent = 0,
        /// <summary>
        /// Serilog: Error, When functionality is unavailable or expectations broken, an Error event is used.
        /// </summary>
        Error = 1,
        /// <summary>
        /// // Serilog: Warning, When service is degraded, endangered, or may be behaving outside of its expected parameters, Warning level events are used.
        /// </summary>
        Warning = 2,
        /// <summary>
        /// Serilog: Information events describe things happening in the system that correspond to its responsibilities and functions. Generally these are the observable actions the system can perform.
        /// </summary>
        Normal = 3,
        /// <summary>
        /// Serilog: Debug is used for internal system events that are not necessarily observable from the outside, but useful when determining how something happened.
        /// </summary>
        Debug = 4,
        /// <summary>
        /// Serilog: Verbose is the noisiest level, rarely (if ever) enabled for a production app.
        /// </summary>
        Verbose = 5
    }

    public class SoundFingerprinter
    {
        public static string DEBUG_DIRECTORY_PATH = "debug";

        // Supported audio files
        private static string[] extensions = { ".wav", ".aif", ".aiff", ".fla", ".flac", ".ogg", ".mp1", ".m1a", ".mp2", ".m2a", ".mp3", ".mpg", ".mpeg", ".mpeg3" };

        private readonly object _lockObj = new object();

        private IModelService modelService;
        private IAudioService audioService;

        private ISpectrumService spectrumService;

        private IFingerprintService fingerprintService;

        private IFingerprintCommandBuilder fingerprintCommandBuilder;

        public SoundFingerprinter() : this(null, null)
        {
        }
        public SoundFingerprinter(string loadFromPath) : this(loadFromPath, null)
        {
        }

        public SoundFingerprinter(string loadFromPath, string debugDirectoryPath)
        {
            if (!string.IsNullOrEmpty(debugDirectoryPath))
            {
                DEBUG_DIRECTORY_PATH = debugDirectoryPath;
            }

            if (!string.IsNullOrEmpty(loadFromPath) && File.Exists(loadFromPath))
            {
                // this.modelService = new InMemoryModelService(loadFromPath);
                this.modelService = new FindSimilarLiteDBService(loadFromPath);
            }
            else
            {
                // this.modelService = new InMemoryModelService();
                this.modelService = new FindSimilarLiteDBService(loadFromPath);
            }

            this.audioService = new FindSimilarAudioService();

            var fingerprintConfig = new ShortSamplesFingerprintConfiguration();
            this.spectrumService = new FindSimilarSpectrumService(
                fingerprintConfig.SpectrogramConfig,
                new LogUtility()
            );
            // this.spectrumService = new SpectrumService(new LomontFFT(), new LogUtility());

            this.fingerprintService = new FindSimilarFingerprintService(
                spectrumService,
                new LocalitySensitiveHashingAlgorithm(new MinHashService(new MaxEntropyPermutations()), new HashConverter()),
                new StandardHaarWaveletDecomposition(),
                new FastFingerprintDescriptor()
            );

            this.fingerprintCommandBuilder = new FingerprintCommandBuilder(fingerprintService);
        }

        public void Snapshot(string saveToPath)
        {
            if (modelService is InMemoryModelService)
            {
                ((InMemoryModelService)modelService).Snapshot(saveToPath);
            }
        }

        public void FingerprintDirectory(string directoryPath, double skipDurationAboveSeconds, Verbosity verbosity)
        {
            var stopWatch = new DebugTimer();
            stopWatch.Start();

            IEnumerable<string> filesAll =
                Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()));

            Log.Information("Found {0} files in scan directory.", filesAll.Count());

            // Get all already processed files stored in the database and store in memory
            // It seems to work well with huge volumes of files (200k)
            IEnumerable<string> filesAlreadyProcessed = modelService.ReadAllTracks().Select(i => i.ISRC);
            Log.Information("Database contains {0} already processed files.", filesAlreadyProcessed.Count());

            // find the files that has not already been added to the database
            List<string> filesRemaining = filesAll.Except(filesAlreadyProcessed).ToList();
            Log.Information("Found {0} files remaining in scan directory to be processed.", filesRemaining.Count);

            int filesCounter = 0;
            int filesAllCounter = filesAlreadyProcessed.Count();

            var options = new ParallelOptions();
#if DEBUG
            // Trick for debugging parallel code as single threaded
            options.MaxDegreeOfParallelism = 1;
            Log.Debug("Running in single-threaded mode!");
#endif
            Parallel.ForEach(filesRemaining, options, file =>
            {
                var fileInfo = new FileInfo(file);

                double duration = 0;
                lock (_lockObj)
                {
                    // Try to check duration
                    try
                    {
                        duration = audioService.GetLengthInSeconds(fileInfo.FullName);
                    }
                    catch (System.Exception e)
                    {
                        // Log
                        Log.Warning(e.Message);
                    }
                }

                // check if we should skip files longer than x seconds
                if ((skipDurationAboveSeconds > 0 && duration > 0 && duration < skipDurationAboveSeconds)
                    || skipDurationAboveSeconds <= 0
                    || duration < 0)
                {
                    // store the full file path in the title field
                    var track = new TrackData(null, null, fileInfo.FullName, null, 0, duration);
                    if (!StoreAudioFileFingerprintsInStorageForLaterRetrieval(file, track, verbosity))
                    {
                        Log.Fatal("Failed! Could not generate audio fingerprint for: {0}", file);
                    }
                    else
                    {
                        // Threadsafe increment
                        // https://pragmaticpattern.wordpress.com/2013/07/03/c-parallel-programming-increment-variable-safely-across-multiple-threads/
                        var filesCounterNow = Interlocked.Increment(ref filesCounter);
                        var filesAllCounterNow = Interlocked.Increment(ref filesAllCounter);
                        Log.Information("[{1}/{2} - {3}/{4}] Added {0} to database. (Thread: {5})", fileInfo.Name, filesCounter, filesRemaining.Count, filesAllCounter, filesAll.Count(), Thread.CurrentThread.ManagedThreadId);
                    }
                }
                else
                {
                    Log.Warning("Skipping file {0} duration: {1}, skip: {2}!", file, duration, skipDurationAboveSeconds);
                }
            });

            Log.Information("Time used: {0}", stopWatch.Stop());
        }

        public bool StoreAudioFileFingerprintsInStorageForLaterRetrieval(string pathToAudioFile, TrackData track, Verbosity verbosity)
        {
            if (track == null) return false;

            lock (_lockObj)
            {
                var fingerprintConfig = new ShortSamplesFingerprintConfiguration();

                // set verbosity
                fingerprintConfig.SpectrogramConfig.Verbosity = verbosity;

                try
                {
                    // create hashed fingerprints
                    var hashedFingerprints = fingerprintCommandBuilder
                                                .BuildFingerprintCommand()
                                                .From(pathToAudioFile)
                                                .WithFingerprintConfig(fingerprintConfig)
                                                .UsingServices(audioService)
                                                .Hash()
                                                .Result;

                    if (hashedFingerprints.Count > 0)
                    {
                        // store track metadata in the datasource
                        var trackReference = modelService.InsertTrack(track);

                        // store hashes in the database for later retrieval
                        modelService.InsertHashDataForTrack(hashedFingerprints, trackReference);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                catch (System.Exception e)
                {
                    // Log
                    Log.Information(e.Message);
                    return false;
                }
            }
        }

        public IEnumerable<ResultEntry> GetBestMatchesForSong(string queryAudioFile, int thresholdVotes, int maxTracksToReturn, Verbosity verbosity)
        {
            lock (_lockObj)
            {
                var queryConfig = new ShortSamplesQueryConfiguration();

                // set verbosity
                queryConfig.FingerprintConfiguration.SpectrogramConfig.Verbosity = verbosity;

                // override threshold and max if they were passed
                if (thresholdVotes > 0) queryConfig.ThresholdVotes = thresholdVotes;
                if (maxTracksToReturn > 0) queryConfig.MaxTracksToReturn = maxTracksToReturn;

                // query the underlying database for similar audio sub-fingerprints
                var queryResult = new QueryCommandBuilder(fingerprintCommandBuilder, QueryFingerprintService.Instance)
                                                     .BuildQueryCommand()
                                                     .From(queryAudioFile)
                                                     .WithQueryConfig(queryConfig)
                                                     .UsingServices(modelService, audioService)
                                                     .Query()
                                                     .Result;

                return queryResult.ResultEntries;
            }
        }
    }
}