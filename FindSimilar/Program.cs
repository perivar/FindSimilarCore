using System;
using McMaster.Extensions.CommandLineUtils;
using Serilog;
using FindSimilarServices;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using FindSimilarServices.Audio;
using CommonUtils.Audio;
using FindSimilarServices.CSCore.Codecs.ADPCM;

namespace FindSimilar
{
    class Program
    {

        const string DATABASE_PATH = @"C:\Users\pnerseth\My Projects\fingerprint.db";

        public static int Main(string[] args)
        {
            var testAudioService = new FindSimilarAudioService();
            int sampleRate = 32000;

            /*             var data1 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Lady Gaga - Marry The Night (Afrojack Remix) Leo Villagra Remake\Yeah fxvoice afrpck 16.wav", sampleRate, 0, 0);
                        SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data1.wav", data1.Samples, sampleRate);

                        var data2 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Lady Gaga - Marry The Night (Afrojack Remix) Leo Villagra Remake\bass afrpck 8.wav", sampleRate, 0, 0);
                        SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data2.wav", data2.Samples, sampleRate);

                        var data3 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Documents\Audacity\bass afrpck 8 - fix.wav", sampleRate, 0, 0);
                        SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data3.wav", data3.Samples, sampleRate);

                        var data4 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\My Projects\snare-ms-adpcm.wav", sampleRate, 0, 0);
                        SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data4.wav", data4.Samples, sampleRate);

                        var data5 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\My Projects\snare-ima-adpcm.wav", sampleRate, 0, 0);
                        SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data5.wav", data5.Samples, sampleRate);

                        testAudioService.ConvertToPCM16Bit(@"C:\Users\pnerseth\My Projects\snare-ima-adpcm.wav", @"C:\Users\pnerseth\My Projects\data5-original.wav");
                        var data6 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Van Halen Jump\FPC_Crash_G16InLite_01.wav", sampleRate, 0, 0);
                        SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data6.wav", data6.Samples, sampleRate);

                        var data7 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!Tutorials\Electro Dance tutorial by Phil Doon\DNC_Kick.wav", sampleRate, 0, 0);
                        SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data7.wav", data7.Samples, sampleRate);

                        return 0;
             */

            Log.Logger = new LoggerConfiguration()
                .WriteTo.File("findsimilar.log")
                .CreateLogger();

            // https://natemcmaster.github.io/CommandLineUtils/
            // https://gist.github.com/iamarcel/8047384bfbe9941e52817cf14a79dc34
            // https://gist.github.com/TerribleDev/06abb67350745a58f9fab080bee74be1#file-program-cs
            var app = new CommandLineApplication();
            app.Name = "FindSimilar";
            app.Description = ".NET Core Find Similar App";
            app.HelpOption();

            app.Command("scan", (command) =>
                {
                    command.Description = "Scan directory and add audio-fingerprints to database (defaults to ignoring already added files)";
                    command.HelpOption();

                    // argument
                    var scanArgument = command.Argument("[directory]", "Directory to scan and create audio fingerprints from");

                    // options
                    var optionSkipDuration = command.Option("-d|--skipduration <NUMBER>", "Skip files longer than x seconds", CommandOptionType.SingleValue);
                    var argumentSilent = command.Option("-s|--silent", "Do not output so much info", CommandOptionType.NoValue);

                    command.OnExecute(() =>
                        {
                            if (!string.IsNullOrEmpty(scanArgument.Value))
                            {
                                ProcessDir(scanArgument.Value, optionSkipDuration.Value());
                                return 0;
                            }
                            else
                            {
                                command.ShowHelp();
                                return 1;
                            }
                        });
                });

            app.Command("match", (command) =>
                {
                    command.Description = "Find matches for specified audio-file";
                    command.HelpOption();

                    // argument
                    var matchArgument = command.Argument("[file path]", "Path to audio-file to find matches for");

                    // options
                    var optionMatchThreshold = command.Option("-t|--threshold <NUMBER>", "Threshold votes for a match. Default: 4", CommandOptionType.SingleValue);
                    var optionMaxNumber = command.Option("-n|--num <NUMBER>", "Maximal number of matches to return when querying. Default: 25", CommandOptionType.SingleValue);

                    command.OnExecute(() =>
                        {
                            if (!string.IsNullOrEmpty(matchArgument.Value))
                            {
                                int threshold = -1;
                                if (optionMatchThreshold.HasValue())
                                {
                                    threshold = int.Parse(optionMatchThreshold.Value());
                                }
                                int maxTracksToReturn = -1;
                                if (optionMaxNumber.HasValue())
                                {
                                    maxTracksToReturn = int.Parse(optionMaxNumber.Value());
                                }

                                MatchFile(matchArgument.Value, threshold, maxTracksToReturn);
                                return 0;
                            }
                            else
                            {
                                command.ShowHelp();
                                return 1;
                            }
                        });
                });

            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 1;
            });

            return app.Execute(args);
        }

        private static void ProcessDir(string directoryPath, string skipDurationAboveSecondsString)
        {
            double skipDurationAboveSeconds = 0;
            if (!string.IsNullOrEmpty(skipDurationAboveSecondsString))
            {
                skipDurationAboveSeconds = double.Parse(skipDurationAboveSecondsString);
            }

            if (Directory.Exists(directoryPath))
            {
                // https://github.com/AddictedCS/soundfingerprinting.duplicatesdetector/blob/master/src/SoundFingerprinting.DuplicatesDetector/DuplicatesDetectorService.cs
                // https://github.com/protyposis/Aurio/tree/master/Aurio/Aurio     
                var fingerprinter = new SoundFingerprinter(DATABASE_PATH);
                fingerprinter.FingerprintDirectory(Path.GetFullPath(directoryPath), skipDurationAboveSeconds);
                fingerprinter.Snapshot(DATABASE_PATH);
            }
            else
            {
                Console.Error.WriteLine("The directory '{0}' cannot be found.", directoryPath);
            }
        }
        private static void MatchFile(string filePath, int thresholdVotes, int maxTracksToReturn)
        {
            if (File.Exists(filePath))
            {
                var fingerprinter = new SoundFingerprinter(DATABASE_PATH);
                var results = fingerprinter.GetBestMatchesForSong(Path.GetFullPath(filePath), thresholdVotes, maxTracksToReturn);

                Console.WriteLine("Found {0} similar tracks", results.Count());
                foreach (var result in results)
                {
                    Console.WriteLine("{0}, confidence {1}, coverage {2}, est. coverage {3}", result.Track.ISRC, result.Confidence, result.Coverage, result.EstimatedCoverage);
                }
            }
            else
            {
                Console.Error.WriteLine("The file '{0}' cannot be found.", filePath);
            }
        }
    }
}
