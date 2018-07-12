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

namespace FindSimilar
{
    class Program
    {
        const string DEFAULT_LOG_PATH = "findsimilar.log";
        const string DATABASE_PATH = @"C:\Users\pnerseth\My Projects\fingerprint.db";

        static Verbosity GetVerbosity(CommandOption verboseOption)
        {
            Verbosity verbosity = Verbosity.Normal;
            if (verboseOption.HasValue())
            {
                Enum.TryParse(verboseOption.Value(), out verbosity);
                if ((int)verbosity > 5) verbosity = Verbosity.Verbose;
                if ((int)verbosity < 0) verbosity = Verbosity.Normal;
            }
            return verbosity;
        }

        static double GetSkipDuration(CommandOption skipDurationOption)
        {
            double skipDurationAboveSeconds = 0;
            var skipDurationAboveSecondsString = skipDurationOption.Value();
            if (!string.IsNullOrEmpty(skipDurationAboveSecondsString))
            {
                skipDurationAboveSeconds = double.Parse(skipDurationAboveSecondsString);
            }
            return skipDurationAboveSeconds;
        }

        static void DefineLogger(CommandOption logFilePathOption, Verbosity verbosity)
        {
            string logFilePath = DEFAULT_LOG_PATH;
            if (logFilePathOption.HasValue())
            {
                var logFilePathValue = logFilePathOption.Value();
                if (Directory.Exists(Path.GetDirectoryName(logFilePathValue)))
                {
                    logFilePath = logFilePathValue;
                }
            }

            // https://github.com/serilog/serilog/wiki/Configuration-Basics
            var logConfig = new LoggerConfiguration()
                .WriteTo.File(logFilePath)
                .WriteTo.Console(); // .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)

            switch (verbosity)
            {
                case Verbosity.Verbose:
                    logConfig.MinimumLevel.Verbose();
                    break;
                case Verbosity.Debug:
                    logConfig.MinimumLevel.Debug();
                    break;
                case Verbosity.Normal:
                    logConfig.MinimumLevel.Information();
                    break;
                case Verbosity.Warning:
                    logConfig.MinimumLevel.Error();
                    break;
                case Verbosity.Error:
                    logConfig.MinimumLevel.Error();
                    break;
                case Verbosity.Silent:
                    logConfig.MinimumLevel.Fatal();
                    break;
            }
            Log.Logger = logConfig.CreateLogger();
        }

        public static int Main(string[] args)
        {
            /*  

            // sox command to test failed audio files
            // sox --i -V6 "FILE"
            var testAudioService = new FindSimilarAudioService();
            int sampleRate = 32000;

            // long adpcm file
            var data11 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Timbaland ft One republic - Apologize\STR_3c_Long.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data11.wav", data11.Samples, sampleRate);

            return 0;

            // OGG within wav container fest files
            var data08 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Van Halen Jump\FPC_Crash_G16InLite_01.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data08.wav", data08.Samples, sampleRate);

            var data09 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!Tutorials\Electro Dance tutorial by Phil Doon\DNC_Kick.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data09.wav", data09.Samples, sampleRate);

            // OGG test files
            var data00 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Avicii - Silhouettes (Melody Remake by EtasDj)\Sweep 1.ogg", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data00.wav", data00.Samples, sampleRate);

            var data01 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Avicii - Silhouettes (Melody Remake by EtasDj)\Crash 2.ogg", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data01.wav", data01.Samples, sampleRate);

            var data02 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!PERIVAR\Jason Derulo In My Head Remix\La Manga.ogg", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data02.wav", data02.Samples, sampleRate);

            // ADPCM test files
            var data03 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Lady Gaga - Marry The Night (Afrojack Remix) Leo Villagra Remake\Yeah fxvoice afrpck 16.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data03.wav", data03.Samples, sampleRate);

            var data04 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Lady Gaga - Marry The Night (Afrojack Remix) Leo Villagra Remake\bass afrpck 8.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data04.wav", data04.Samples, sampleRate);

            var data05 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Documents\Audacity\bass afrpck 8 - fix.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data05.wav", data05.Samples, sampleRate);

            var data06 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\My Projects\snare-ms-adpcm.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data06.wav", data06.Samples, sampleRate);

            var data07 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\My Projects\snare-ima-adpcm.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data07.wav", data07.Samples, sampleRate);

            // Wav 24 bit extensible format
            var data10 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!Preset_Template\HTMEM-Another-Walkthrough-To-An-EDM-Beat\mhak kick 209 G#.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data10.wav", data10.Samples, sampleRate);

                        return 0;
             */
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
                    var skipDurationOption = command.Option("-d|--skipduration <NUMBER>", "Skip files longer than x seconds", CommandOptionType.SingleValue);
                    var verboseOption = command.Option("-v|--verbose <NUMBER>", "Increase the verbosity of messages: 0 for silent mode, 3 for normal output, 4 for debug and 5 for the most verbose setting", CommandOptionType.SingleValue);
                    var logDirOption = command.Option("-l|--log <path>", "Path to log-file", CommandOptionType.SingleValue);

                    command.OnExecute(() =>
                        {
                            if (!string.IsNullOrEmpty(scanArgument.Value))
                            {
                                var skipDurationAboveSeconds = GetSkipDuration(skipDurationOption);
                                var verbosity = GetVerbosity(verboseOption);
                                DefineLogger(logDirOption, verbosity);

                                ProcessDir(scanArgument.Value, skipDurationAboveSeconds, verbosity);
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

            try
            {
                return app.Execute(args);
            }
            catch (System.Exception)
            {
                Console.WriteLine("Command not recognized {0}.", args);
                return 1;
            }
        }

        private static void ProcessDir(string directoryPath, double skipDurationAboveSeconds, Verbosity verbosity)
        {
            if (Directory.Exists(directoryPath))
            {
                // https://github.com/AddictedCS/soundfingerprinting.duplicatesdetector/blob/master/src/SoundFingerprinting.DuplicatesDetector/DuplicatesDetectorService.cs
                // https://github.com/protyposis/Aurio/tree/master/Aurio/Aurio     
                var fingerprinter = new SoundFingerprinter(DATABASE_PATH);
                fingerprinter.FingerprintDirectory(Path.GetFullPath(directoryPath), skipDurationAboveSeconds, verbosity);
                fingerprinter.Snapshot(DATABASE_PATH);
            }
            else
            {
                if (verbosity > 0) Console.Error.WriteLine("The directory '{0}' cannot be found.", directoryPath);
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
