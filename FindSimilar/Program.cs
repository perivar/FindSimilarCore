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
using SoundFingerprinting;

namespace FindSimilar
{
    class Program
    {
        const string DEFAULT_LOG_PATH = "findsimilar.log";
        const string DEFAULT_ERROR_LOG_PATH = "findsimilar_error.log";
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

        static void DefineLogger(CommandOption logFilePathOption, CommandOption errorLogFilePathOption, Verbosity verbosity)
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

            string errorLogFilePath = DEFAULT_ERROR_LOG_PATH;
            if (errorLogFilePathOption.HasValue())
            {
                // if the directory for the log file exists, we can create it later if it doesn't exist
                var errorLogFilePathValue = errorLogFilePathOption.Value();
                if (Directory.Exists(Path.GetDirectoryName(errorLogFilePathValue)))
                {
                    errorLogFilePath = errorLogFilePathValue;
                }
            }

            // https://github.com/serilog/serilog/wiki/Configuration-Basics
            var logConfig = new LoggerConfiguration()
                .WriteTo.File(logFilePath)
                .WriteTo.Console() // .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
                .WriteTo.Logger(l => l.Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Fatal).WriteTo.File(errorLogFilePath));

            switch (verbosity)
            {
                case Verbosity.Verbose:
                    logConfig.MinimumLevel.Verbose();
                    logConfig.MinimumLevel.Override("Microsoft", LogEventLevel.Information); // Don't ouput more  
                    break;
                case Verbosity.Debug:
                    logConfig.MinimumLevel.Debug();
                    logConfig.MinimumLevel.Override("Microsoft", LogEventLevel.Information); // Don't ouput more  
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
                    var errorLogDirOption = command.Option("-e|--elog <path>", "Path to error log-file", CommandOptionType.SingleValue);
                    var dbDirOption = command.Option("-d|--db <path>", "Override the default path to database-file: " + DEFAULT_DATABASE_PATH, CommandOptionType.SingleValue);
                    var debugDirOption = command.Option("--debug <path>", "If verbose = 5, override the default path to the debug directory: " + DEFAULT_DEBUG_PATH, CommandOptionType.SingleValue);

                    command.OnExecute(() =>
                        {
                            if (!string.IsNullOrEmpty(scanArgument.Value))
                            {
                                var skipDurationAboveSeconds = GetSkipDuration(skipDurationOption);
                                var verbosity = GetVerbosity(verboseOption);
                                DefineLogger(logDirOption, errorLogDirOption, verbosity);
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
                    var errorLogDirOption = command.Option("-e|--elog <path>", "Path to error log-file", CommandOptionType.SingleValue);
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
                                DefineLogger(logDirOption, errorLogDirOption, verbosity);
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
            catch (System.Exception e)
            {
                Console.WriteLine("Error using command {0}. [{1}].", args, e.Message);
                return 1;
            }
        }

        private static void ProcessDir(string dbPath, string directoryPath, double skipDurationAboveSeconds, Verbosity verbosity, string debugDirectoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                // use LiteDb
                var model = new FindSimilarLiteDBService(dbPath);
                var fingerprinter = new SoundFingerprinter(model, debugDirectoryPath);

                // use SQLite
                // var fingerprinter = new SoundFingerprinter(dbPath, debugDirectoryPath);

                fingerprinter.FingerprintDirectory(Path.GetFullPath(directoryPath), skipDurationAboveSeconds, verbosity);
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
