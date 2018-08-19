using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Serilog;

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
            if (string.IsNullOrEmpty(_connectionString))
            {
                LoadConnectionString(args);
            }

            Log.Information($"Using connection string {_connectionString}");

            DbContextOptionsBuilder<SQLiteDbContext> optionsBuilder =
                        new DbContextOptionsBuilder<SQLiteDbContext>()
                            .UseSqlite(_connectionString);

            return new SQLiteDbContext(optionsBuilder.Options);
        }

        private void LoadConnectionString(string[] args)
        {
            Dictionary<string, string> inMemoryCollection = new Dictionary<string, string>();

            string message = "";
            if (args.Any())
            {
                // Connection strings has keys like "ConnectionStrings:DefaultConnection" 
                // and values like "Data Source=C:\\Users\\pnerseth\\My Projects\\fingerprint.db"
                message = $"Searching for '{CONNECTION_STRING_KEY}' within passed arguments: {string.Join(", ", args)}";
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
                message = $"Searching for '{CONNECTION_STRING_KEY}' in {Directory.GetCurrentDirectory()} => appsettings.json";
            }
            Log.Information(message);
            Console.WriteLine(message);

            var configurationBuilder = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddInMemoryCollection(inMemoryCollection);

            IConfigurationRoot configuration = configurationBuilder.Build();
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }
    }
}