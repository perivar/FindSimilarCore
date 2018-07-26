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
using Serilog.Events;

namespace FindSimilar
{
    class Program
    {
        const string DEFAULT_LOG_PATH = "findsimilar.log";
        const string DEFAULT_DATABASE_PATH = "fingerprint.db";
        const string DEFAULT_DEBUG_PATH = "debug";

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

        static string GetDatabaseFilePath(CommandOption dbFilePathOption)
        {
            string dbFilePath = DEFAULT_DATABASE_PATH;
            if (dbFilePathOption.HasValue())
            {
                // if the directory for the database file exists, we can create it later if it doesn't exist
                var dbFilePathValue = dbFilePathOption.Value();
                if (Directory.Exists(Path.GetDirectoryName(dbFilePathValue)))
                {
                    dbFilePath = dbFilePathValue;
                }
            }
            return dbFilePath;
        }

        static string GetDebugDirectoryPath(CommandOption debugDirectoryPathOption)
        {
            string debugDirectoryPath = DEFAULT_DEBUG_PATH;
            if (debugDirectoryPathOption.HasValue())
            {
                // check that the directory for the debug files exists
                var debugDirectoryPathValue = debugDirectoryPathOption.Value();
                if (Directory.Exists(Path.GetDirectoryName(debugDirectoryPathValue)))
                {
                    debugDirectoryPath = debugDirectoryPathValue;
                }
            }

            return debugDirectoryPath;
        }

        static void DefineLogger(CommandOption logFilePathOption, Verbosity verbosity)
        {
            string logFilePath = DEFAULT_LOG_PATH;
            if (logFilePathOption.HasValue())
            {
                // if the directory for the log file exists, we can create it later if it doesn't exist
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
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console()
                .CreateLogger();

            // mp3 file that fails due to frame errors (quantization error)
            var data20 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Britney Spears - 3\03 Britney Spears - 3 (Acapella).mp3", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data20-mp3.wav", data20.Samples, sampleRate);

            return 0;

            // wav files with LIST chunk            
            var data18 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Lady Gaga - Poker Face\FPC_Crash_G16InLite_05.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data18-LIST-chunk.wav", data18.Samples, sampleRate);

            var data19 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!PERIVAR\Clean Bandit - Rather Be Programming\Clean Bandit - Region 1 Slow.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data19-LIST-chunk.wav", data19.Samples, sampleRate);

            // wav files with PAD sub chunk
            var data16 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Black Eyed Peas - The Time Mehran Abbasi Reworked Final/VEH3 Snares 026.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data16-pad-chunk.wav", data16.Samples, sampleRate);

            var data17 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!PERIVAR\Lynda EDM Drums\Crash.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data17-pad-chunk.wav", data17.Samples, sampleRate);


            // OGG test files
            var data00 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Avicii - Silhouettes (Melody Remake by EtasDj)\Sweep 1.ogg", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data00-ogg.wav", data00.Samples, sampleRate);

            var data01 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Avicii - Silhouettes (Melody Remake by EtasDj)\Crash 2.ogg", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data01-ogg.wav", data01.Samples, sampleRate);

            var data02 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!PERIVAR\Jason Derulo In My Head Remix\La Manga.ogg", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data02-ogg.wav", data02.Samples, sampleRate);

            // ADPCM test files
            var data03 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Lady Gaga - Marry The Night (Afrojack Remix) Leo Villagra Remake\Yeah fxvoice afrpck 16.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data03-adpcm.wav", data03.Samples, sampleRate);

            // @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Lady Gaga - Marry The Night (Afrojack Remix) Leo Villagra Remake\clap afrpck 5.wav"

            var data04 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Lady Gaga - Marry The Night (Afrojack Remix) Leo Villagra Remake\bass afrpck 8.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data04-adpcm.wav", data04.Samples, sampleRate);

            var data05 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Documents\Audacity\bass afrpck 8 - fix.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data05-adpcm.wav", data05.Samples, sampleRate);

            var data06 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\My Projects\snare-ms-adpcm.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data06-adpcm.wav", data06.Samples, sampleRate);

            var data07 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\My Projects\snare-ima-adpcm.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data07-adpcm.wav", data07.Samples, sampleRate);

            // OGG within wav container fest files
            var data08 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Van Halen Jump\FPC_Crash_G16InLite_01.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data08-oggwav.wav", data08.Samples, sampleRate);

            var data09 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!Tutorials\Electro Dance tutorial by Phil Doon\DNC_Kick.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data09-oggwav.wav", data09.Samples, sampleRate);

            // Wav 24 bit extensible format
            var data10 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!Preset_Template\HTMEM-Another-Walkthrough-To-An-EDM-Beat\mhak kick 209 G#.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data10-24bit-extensible.wav", data10.Samples, sampleRate);

            // long adpcm file
            var data11 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Timbaland ft One republic - Apologize\STR_3c_Long.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data11-adpcm-long.wav", data11.Samples, sampleRate);

            // MuLaw files
            var data12 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Ed Sheeran - Shape Of You\Snare (2).wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data12-mulaw.wav", data12.Samples, sampleRate);

            testAudioService.ConvertToPCM16Bit(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!Preset_Template\FSS Essential Collection Vol 2\Kraftwerk - The Robots (Album Mix)\cow bell001.wav", @"C:\Users\pnerseth\My Projects\data13-original-mulaw.wav");
            var data13 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!Preset_Template\FSS Essential Collection Vol 2\Kraftwerk - The Robots (Album Mix)\cow bell001.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data13-mulaw.wav", data13.Samples, sampleRate);

            var data14 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Rihanna - We Found Love ft. Calvin Harris by www.aronize.tk\DNC_Hat_4.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data14-mulaw.wav", data14.Samples, sampleRate);

            // 24 bit PCM files
            var data15 = testAudioService.ReadMonoSamplesFromFile(@"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!PERIVAR\House Baerum\ATE Reverb Kick - 003.wav", sampleRate, 0, 0);
            SoundIO.WriteWaveFile(@"C:\Users\pnerseth\My Projects\data15-24bit.wav", data15.Samples, sampleRate);

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
                    var skipDurationOption = command.Option("-s|--skipduration <NUMBER>", "Skip files longer than x seconds", CommandOptionType.SingleValue);
                    var verboseOption = command.Option("-v|--verbose <NUMBER>", "Increase the verbosity of messages: 0 for silent mode, 3 for normal output, 4 for debug and 5 for the most verbose setting", CommandOptionType.SingleValue);
                    var logDirOption = command.Option("-l|--log <path>", "Path to log-file", CommandOptionType.SingleValue);
                    var dbDirOption = command.Option("-d|--db <path>", "Override the default path to database-file: " + DEFAULT_DATABASE_PATH, CommandOptionType.SingleValue);
                    var debugDirOption = command.Option("--debug <path>", "If verbose = 5, override the default path to the debug directory: " + DEFAULT_DEBUG_PATH, CommandOptionType.SingleValue);

                    command.OnExecute(() =>
                        {
                            if (!string.IsNullOrEmpty(scanArgument.Value))
                            {
                                var skipDurationAboveSeconds = GetSkipDuration(skipDurationOption);
                                var verbosity = GetVerbosity(verboseOption);
                                DefineLogger(logDirOption, verbosity);
                                var dbPath = GetDatabaseFilePath(dbDirOption);
                                var debugPath = GetDebugDirectoryPath(debugDirOption);

                                ProcessDir(dbPath, scanArgument.Value, skipDurationAboveSeconds, verbosity, debugPath);
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
                    var dbDirOption = command.Option("-d|--db <path>", "Override the default path to database-file: " + DEFAULT_DATABASE_PATH, CommandOptionType.SingleValue);
                    var verboseOption = command.Option("-v|--verbose <NUMBER>", "Increase the verbosity of messages: 0 for silent mode, 3 for normal output, 4 for debug and 5 for the most verbose setting", CommandOptionType.SingleValue);
                    var logDirOption = command.Option("-l|--log <path>", "Path to log-file", CommandOptionType.SingleValue);
                    var debugDirOption = command.Option("--debug <path>", "If verbose = 5, override the default path to the debug directory: " + DEFAULT_DEBUG_PATH, CommandOptionType.SingleValue);

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

                                var verbosity = GetVerbosity(verboseOption);
                                DefineLogger(logDirOption, verbosity);
                                var dbPath = GetDatabaseFilePath(dbDirOption);
                                var debugPath = GetDebugDirectoryPath(debugDirOption);

                                MatchFile(dbPath, matchArgument.Value, threshold, maxTracksToReturn, verbosity, debugPath);
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

        private static void ProcessDir(string dbPath, string directoryPath, double skipDurationAboveSeconds, Verbosity verbosity, string debugDirectoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                var fingerprinter = new SoundFingerprinter(dbPath, debugDirectoryPath);
                fingerprinter.FingerprintDirectory(Path.GetFullPath(directoryPath), skipDurationAboveSeconds, verbosity);
                fingerprinter.Snapshot(dbPath);
            }
            else
            {
                if (verbosity > 0) Console.Error.WriteLine("The directory '{0}' cannot be found.", directoryPath);
            }
        }
        private static void MatchFile(string dbPath, string filePath, int thresholdVotes, int maxTracksToReturn, Verbosity verbosity, string debugDirectoryPath)
        {
            if (File.Exists(filePath))
            {
                var fingerprinter = new SoundFingerprinter(dbPath, debugDirectoryPath);
                var results = fingerprinter.GetBestMatchesForSong(Path.GetFullPath(filePath), thresholdVotes, maxTracksToReturn, verbosity);

                Console.WriteLine("Found {0} similar tracks", results.Count());

                // results.BestMatch.Track
                foreach (var result in results)
                {
                    // the track title holds the full filename                     
                    FileInfo fileInfo = new FileInfo(result.Track.Title);

                    Console.WriteLine("{0}, confidence {1}, coverage {2}, est. coverage {3}", fileInfo.FullName, result.Confidence, result.Coverage, result.EstimatedCoverage);
                }
            }
            else
            {
                Console.Error.WriteLine("The file '{0}' cannot be found.", filePath);
            }
        }
    }
}
