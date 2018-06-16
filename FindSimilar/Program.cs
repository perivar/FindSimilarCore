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

namespace FindSimilar
{
    class Program
    {

        const string DATABASE_PATH = @"C:\Users\pnerseth\My Projects\fingerprint.db";

        public static int Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File("findsimilar.log")
                .CreateLogger();

            // https://natemcmaster.github.io/CommandLineUtils/
            // https://gist.github.com/iamarcel/8047384bfbe9941e52817cf14a79dc34
            var app = new CommandLineApplication();
            app.Name = "FindSimilar";
            app.Description = ".NET Core Find Similar App";
            app.HelpOption();

            var optionScanDir = app.Option("-s|--scandir <DIR>", "Scan directory path and create audio fingerprints (ignore existing files)", CommandOptionType.SingleValue);
            var optionMatchFile = app.Option("-m|--match <FILE>", "Path to the wave file to find matches for", CommandOptionType.SingleValue);
            var optionSkipDuration = app.Option("-d|--skipduration <NUMBER>", "Skip files longer than x seconds, used together with scandir", CommandOptionType.SingleValue);
            var optionTake = app.Option("-t|--take <NUMBER>", "Number of matches to return when querying", CommandOptionType.SingleValue);
            var argumentSilent = app.Argument("--silent", "Do not output so much info, used together with scandir");

            app.OnExecute(() =>
            {
                if (optionScanDir.HasValue())
                {
                    ProcessDir(optionScanDir.Value());
                    return 0;
                }

                if (optionMatchFile.HasValue())
                {
                    MatchFile(optionMatchFile.Value());
                    return 0;
                }

                app.ShowHelp();
                return 0;
            });

            return app.Execute(args);
        }

        private static void ProcessDir(string directoryPath)
        {
            double skipDurationAboveSeconds = 30;

            // https://github.com/AddictedCS/soundfingerprinting.duplicatesdetector/blob/master/src/SoundFingerprinting.DuplicatesDetector/DuplicatesDetectorService.cs
            // https://github.com/protyposis/Aurio/tree/master/Aurio/Aurio     
            var fingerprinter = new SoundFingerprinter(DATABASE_PATH);
            fingerprinter.FingerprintDirectory(directoryPath, skipDurationAboveSeconds);
            fingerprinter.Snapshot(DATABASE_PATH);
        }
        private static void MatchFile(string filePath)
        {
            var fingerprinter = new SoundFingerprinter(DATABASE_PATH);
            fingerprinter.GetBestMatchesForSong(filePath);
        }
    }
}
