using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using FindSimilarServices.Fingerprinting;

namespace FindSimilarServices.Fingerprinting.SQLiteDb
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SQLiteDbContext>
    {
        const string CONNECTION_STRING_KEY = "DefaultConnection";
        private string _connectionString;

        public SQLiteDbContext CreateDbContext()
        {
            return CreateDbContext(null);
        }

        public SQLiteDbContext CreateDbContext(string[] args)
        {
            return CreateDbContext(args, Log.Logger);
        }

        public SQLiteDbContext CreateDbContext(string[] args, Serilog.ILogger log)
        {
            // set logging
            ILoggerFactory loggerFactory = new LoggerFactory();

            // this is only null when called from 'dotnet ef migrations ...'
            if (log == null)
            {
                log = new Serilog.LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Console()
                    .CreateLogger();
            }

            DbContextOptionsBuilder<SQLiteDbContext> options =
                        new DbContextOptionsBuilder<SQLiteDbContext>();

            // since Entity Framework outputs so much information at even Information level
            // only output to serilog if log level is debug or lower
            if (log.IsEnabled(LogEventLevel.Debug) || log.IsEnabled(LogEventLevel.Verbose))
            {
                // Disable client evalution in development environment
                options.UseSerilog(loggerFactory, throwOnQueryWarnings: true);

                // add this line to output Entity Framework log statements
                loggerFactory.AddSerilog(log);
            }

            if (string.IsNullOrEmpty(_connectionString))
            {
                LoadConnectionString(args);
            }

            Log.Information($"Using connection string {_connectionString}");

            options.UseSqlite(_connectionString); // default added as Scoped

            return new SQLiteDbContext(options.Options);
        }

        private void LoadConnectionString(string[] args)
        {
            Dictionary<string, string> inMemoryCollection = new Dictionary<string, string>();

            if (args.Any())
            {
                // Connection strings has keys like "ConnectionStrings:DefaultConnection" 
                // and values like "Data Source=C:\\Users\\pnerseth\\My Projects\\fingerprint.db"
                Log.Information($"Searching for '{CONNECTION_STRING_KEY}' within passed arguments: {string.Join(", ", args)}");
                var match = args.FirstOrDefault(s => s.Contains($"ConnectionStrings:{CONNECTION_STRING_KEY}"));
                if (match != null)
                {
                    Regex pattern = new Regex($"(?<name>ConnectionStrings:{CONNECTION_STRING_KEY})=(?<value>.+?)$");

                    inMemoryCollection = Enumerable.ToDictionary(
                      Enumerable.Cast<Match>(pattern.Matches(match)),
                      m => m.Groups["name"].Value,
                      m => m.Groups["value"].Value);
                }
            }
            else
            {
                Log.Information($"Searching for '{CONNECTION_STRING_KEY}' in {Directory.GetCurrentDirectory()} => appsettings.json");
            }

            var configurationBuilder = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddInMemoryCollection(inMemoryCollection);

            IConfigurationRoot configuration = configurationBuilder.Build();
            _connectionString = configuration.GetConnectionString(CONNECTION_STRING_KEY);
        }
    }
}