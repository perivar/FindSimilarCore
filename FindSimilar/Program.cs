using System;
using McMaster.Extensions.CommandLineUtils;
using Serilog;

namespace FindSimilar
{
    class Program
    {
        public static int Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File("findsimilar.log")
                .CreateLogger();

            // https://natemcmaster.github.io/CommandLineUtils/
            // https://gist.github.com/iamarcel/8047384bfbe9941e52817cf14a79dc34
            var app = new CommandLineApplication();

            app.HelpOption();
            var optionSubject = app.Option("-s|--subject <SUBJECT>", "The subject", CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
                var subject = optionSubject.HasValue()
                    ? optionSubject.Value()
                    : "world";

                Console.WriteLine($"Hello {subject}!");
                return 0;
            });

            return app.Execute(args);
        }
    }
}
