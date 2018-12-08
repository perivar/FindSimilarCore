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
using SoundFingerprinting.Query;
using SoundFingerprinting.Strides;
using SoundFingerprinting.FFT;
using SoundFingerprinting.LSH;
using SoundFingerprinting.MinHash;
using SoundFingerprinting.Wavelets;
using SoundFingerprinting.Utils;
using SoundFingerprinting.Math;
using FindSimilarServices.Audio;
using CommonUtils;
using CommonUtils.Audio;
using Serilog;
using FindSimilarServices.Fingerprinting.SQLiteDb;
using Microsoft.EntityFrameworkCore;
using FindSimilarServices.Fingerprinting;
using FindSimilarServices.Fingerprinting.SQLiteDBService;
using Serilog.Events;

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

    public class SoundFingerprinter : ISoundFingerprinter
    {
        public static string DEBUG_DIRECTORY_PATH = "debug";

        // Supported audio files
        private static string[] extensions = { ".wav", ".aif", ".aiff", ".fla", ".flac", ".ogg", ".mp1", ".m1a", ".mp2", ".m2a", ".mp3", ".mpg", ".mpeg", ".mpeg3" };
        private readonly object _lockObj = new object();

        // services used by the fingerprint methods
        private readonly IModelService modelService;
        private readonly IAudioService audioService;
        private readonly ISpectrumService spectrumService;
        private readonly IFingerprintService fingerprintService;
        private readonly IFingerprintCommandBuilder fingerprintCommandBuilder;

        public SoundFingerprinter(IModelService modelService) : this(modelService, null, null)
        {
        }

        public SoundFingerprinter(string loadFromPath, string debugDirectoryPath) : this(null, loadFromPath, debugDirectoryPath)
        {
        }

        public SoundFingerprinter(IModelService modelService, string debugDirectoryPath) : this(modelService, null, debugDirectoryPath)
        {
        }

        public SoundFingerprinter(IModelService modelService, string loadFromPath, string debugDirectoryPath)
        {
            SetDebugPath(debugDirectoryPath);

            // if the modelService was passed, use it
            if (modelService != null)
            {
                this.modelService = modelService;
            }
            else
            {
                //  ... otherwise use the loadFromPath
                this.modelService = GetSQLiteDatabaseService(loadFromPath);
            }

            // and set the rest of the services
            this.audioService = new FindSimilarAudioService();

            var fingerprintConfig = new ShortSamplesFingerprintConfiguration();

            this.spectrumService = new FindSimilarSpectrumService(
                fingerprintConfig.SpectrogramConfig,
                new LogUtility()
            );

            this.fingerprintService = new FindSimilarFingerprintService(
                spectrumService,
                new LocalitySensitiveHashingAlgorithm(new MinHashService(new MaxEntropyPermutations()), new HashConverter()),
                new StandardHaarWaveletDecomposition(),
                new FastFingerprintDescriptor()
            );

            this.fingerprintCommandBuilder = new FingerprintCommandBuilder(fingerprintService);
        }

        private void SetDebugPath(string debugDirectoryPath = null)
        {
            if (!string.IsNullOrEmpty(debugDirectoryPath))
            {
                DEBUG_DIRECTORY_PATH = debugDirectoryPath;
            }

            // create the debug directory if it doesn't exist
            if (!Directory.Exists(DEBUG_DIRECTORY_PATH)) Directory.CreateDirectory(DEBUG_DIRECTORY_PATH);
        }

        private IModelService GetSQLiteDatabaseService(string loadFromPath)
        {
            if (!string.IsNullOrEmpty(loadFromPath))
            {
                if (File.Exists(loadFromPath))
                {
                    var dbContextFactory = new DesignTimeDbContextFactory();
                    var args = new string[] { $"ConnectionStrings:DefaultConnection=Data Source={loadFromPath}" };
                    SQLiteDbContext context = dbContextFactory.CreateDbContext(args);

                    // update
                    context.Database.Migrate();

                    return new FindSimilarSQLiteService(context);
                }
                else
                {
                    var dbContextFactory = new DesignTimeDbContextFactory();
                    var args = new string[] { $"ConnectionStrings:DefaultConnection=Data Source={loadFromPath}" };
                    SQLiteDbContext context = dbContextFactory.CreateDbContext(args);

                    // create
                    context.Database.Migrate();

                    return new FindSimilarSQLiteService(context);
                }
            }
            return null;
        }

        public void FingerprintDirectory(string directoryPath, double skipDurationAboveSeconds, Verbosity verbosity)
        {
            using (new DebugTimer("Fingerprinting Done", LogEventLevel.Information))
            {
                // use List<string> instead of IEnumerable<string> to force iteration
                // and avoid iterating twice due to the count() and the foreach

                List<string> filesAll =
                    Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
                    .Where(f => extensions.Contains(Path.GetExtension(f).ToLower())).ToList();

                int filesAllTotalCount = filesAll.Count();
                Log.Information("Found {0} files in scan directory.", filesAllTotalCount);

                Log.Information("Please wait while we are reading the tracks from the database ...");
                // Get all already processed files stored in the database and store in memory
                // It seems to work well with huge volumes of files (200k)
                List<string> filesAlreadyProcessed = null;
                if (modelService is FindSimilarLiteDBService)
                {
                    filesAlreadyProcessed = ((FindSimilarLiteDBService)modelService).ReadAllTrackFilePaths().ToList();
                }
                else
                {
                    filesAlreadyProcessed = modelService.ReadAllTracks().Select(i => i.Title).ToList();
                }


                int filesAllCounter = filesAlreadyProcessed.Count();
                Log.Information("Database contains {0} already processed files.", filesAllCounter);

                Log.Information("Please wait while we are finding tracks that has not already been added to the database ...");
                // find the files that has not already been added to the database
                List<string> filesRemaining = filesAll.Except(filesAlreadyProcessed).ToList();
                int filesRemainingTotalCount = filesRemaining.Count();
                Log.Information("Found {0} files remaining in scan directory to be processed.", filesRemainingTotalCount);

                var options = new ParallelOptions();
#if DEBUG
                // Trick for debugging parallel code as single threaded
                options.MaxDegreeOfParallelism = 1;
                Log.Debug("Running in single-threaded mode!");
#endif
                int filesRemainingCounter = 0;
                Parallel.ForEach(filesRemaining, options, file =>
                {
                    var fileInfo = new FileInfo(file);
                    double duration = 0;
                    try
                    {
                        duration = audioService.GetLengthInSeconds(fileInfo.FullName);
                    }
                    catch (System.Exception)
                    {
                        Log.Warning("Unable to get duration for: {0}", fileInfo.FullName);
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
                            var filesCounterNow = Interlocked.Increment(ref filesRemainingCounter);
                            var filesAllCounterNow = Interlocked.Increment(ref filesAllCounter);
                            Log.Information("[{1}/{2} - {3}/{4}] Added {0}. {6:0.00} seconds (Thread: {5})", fileInfo.Name, filesRemainingCounter, filesRemainingTotalCount, filesAllCounter, filesAllTotalCount, Thread.CurrentThread.ManagedThreadId, duration);
                        }
                    }
                    else
                    {
                        Log.Warning("Skipping file {0} duration: {1:0.00} sec, skip: {2}!", file, duration, skipDurationAboveSeconds);
                    }
                });
            }
        }

        public bool StoreAudioFileFingerprintsInStorageForLaterRetrieval(string pathToAudioFile, TrackData track, Verbosity verbosity)
        {
            if (track == null) return false;

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
                    lock (_lockObj)
                    {
                        // store track metadata in the datasource
                        var trackReference = modelService.InsertTrack(track);

                        // store hashes in the database for later retrieval
                        modelService.InsertHashDataForTrack(hashedFingerprints, trackReference);
                    }
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

        public IEnumerable<ResultEntry> GetBestMatchesForSong(string queryAudioFile, int thresholdVotes, int maxTracksToReturn, Verbosity verbosity)
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