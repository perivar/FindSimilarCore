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

namespace FindSimilarServices
{
    public class SoundFingerprinter
    {
        // Supported audio files
        //private static string[] extensions = { ".wav", ".ogg", ".mp1", ".m1a", ".mp2", ".m2a", ".mpa", ".mus", ".mp3", ".mpg", ".mpeg", ".mp3pro", ".aif", ".aiff", ".bwf", ".wma", ".wmv", ".aac", ".adts", ".mp4", ".m4a", ".m4b", ".mod", ".mdz", ".mo3", ".s3m", ".s3z", ".xm", ".xmz", ".it", ".itz", ".umx", ".mtm", ".flac", ".fla", ".oga", ".ogg", ".aac", ".m4a", ".m4b", ".mp4", ".mpc", ".mp+", ".mpp", ".ac3", ".wma", ".ape", ".mac" };
        private static string[] extensions = { ".wav", ".aif", ".aiff", ".fla", ".flac", ".ogg" };

        private IModelService modelService;
        private IAudioService audioService;

        private ISpectrumService spectrumService;

        private IFingerprintService fingerprintService;

        private IFingerprintCommandBuilder fingerprintCommandBuilder;

        public SoundFingerprinter() : this(null)
        {
        }
        public SoundFingerprinter(string loadFromPath)
        {
            if (!string.IsNullOrEmpty(loadFromPath) && File.Exists(loadFromPath))
            {
                this.modelService = new InMemoryModelService(loadFromPath);
            }
            else
            {
                this.modelService = new InMemoryModelService();
            }

            this.audioService = new FindSimilarAudioService();

            this.spectrumService = new FindSimilarSpectrumService(
                new LomontFFT(),
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

        public void Snapshot(string saveToPath)
        {
            ((InMemoryModelService)modelService).Snapshot(saveToPath);
        }

        public void FingerprintDirectory(string directoryPath, double skipDurationAboveSeconds)
        {
            var stopWatch = Stopwatch.StartNew();

            IEnumerable<string> filesAll =
                Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()));
            Console.Out.WriteLine("Found {0} files in scan directory.", filesAll.Count());

            // Get all already processed files stored in the database and store in memory
            // It seems to work well with huge volumes of files (200k)
            IEnumerable<string> filesAlreadyProcessed = modelService.ReadAllTracks().Select(i => i.ISRC);
            Console.Out.WriteLine("Database contains {0} already processed files.", filesAlreadyProcessed.Count());

            // find the files that has not already been added to the database
            List<string> filesRemaining = filesAll.Except(filesAlreadyProcessed).ToList();
            Console.Out.WriteLine("Found {0} files remaining in scan directory to be processed.", filesRemaining.Count);

            int filesCounter = 0;
            int filesAllCounter = filesAlreadyProcessed.Count();

            var options = new ParallelOptions();
#if DEBUG
            // Trick for debugging parallel code as single threaded
            options.MaxDegreeOfParallelism = 1;
            Console.Out.WriteLine("Running in single-threaded mode!");
#endif
            Parallel.ForEach(filesRemaining, options, file =>
            {
                var fileInfo = new FileInfo(file);

                // Try to check duration
                double duration = audioService.GetLengthInSeconds(fileInfo.FullName);
                
                // check if we should skip files longer than x seconds
                if ((skipDurationAboveSeconds > 0 && duration > 0 && duration < skipDurationAboveSeconds)
                    || skipDurationAboveSeconds <= 0
                    || duration < 0)
                {
                    var track = new TrackData(fileInfo.FullName, null, fileInfo.Name, null, 0, duration);
                    if (!StoreAudioFileFingerprintsInStorageForLaterRetrieval(file, track))
                    {
                        Console.Out.WriteLine("Failed! Could not generate audio fingerprint for {0}!", file);
                    }
                    else
                    {
                        // Threadsafe increment
                        // https://pragmaticpattern.wordpress.com/2013/07/03/c-parallel-programming-increment-variable-safely-across-multiple-threads/
                        var filesCounterNow = Interlocked.Increment(ref filesCounter);
                        var filesAllCounterNow = Interlocked.Increment(ref filesAllCounter);
                        Console.Out.WriteLine("[{1}/{2} - {3}/{4}] Successfully added {0} to database. (Thread: {5})", fileInfo.Name, filesCounter, filesRemaining.Count, filesAllCounter, filesAll.Count(), Thread.CurrentThread.ManagedThreadId);
                    }
                } else {
                    Console.Out.WriteLine("Skipping file {0} duration: {1}, skip: {2}!", file, duration, skipDurationAboveSeconds);
                }
            });

            Console.WriteLine("Time used: {0}", stopWatch.Elapsed);
        }

        public bool StoreAudioFileFingerprintsInStorageForLaterRetrieval(string pathToAudioFile, TrackData track)
        {
            if (track == null) return false;

            var fingerprintConfig = new ShortSamplesFingerprintConfiguration();

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

        public IEnumerable<ResultEntry> GetBestMatchesForSong(string queryAudioFile)
        {
            var queryConfig = new ShortSamplesQueryConfiguration();

            // query the underlying database for similar audio sub-fingerprints
            var queryResult = new QueryCommandBuilder(fingerprintCommandBuilder, QueryFingerprintService.Instance)
                                                 .BuildQueryCommand()
                                                 .From(queryAudioFile)
                                                 .WithQueryConfig(queryConfig)
                                                 .UsingServices(modelService, audioService)
                                                 .Query()
                                                 .Result;

            //return queryResult.BestMatch.Track; // successful match has been found
            return queryResult.ResultEntries;
        }
    }
}