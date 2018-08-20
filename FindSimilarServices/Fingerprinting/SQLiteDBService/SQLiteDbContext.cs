using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FindSimilarServices.Fingerprinting.SQLiteDb.Models;

namespace FindSimilarServices.Fingerprinting.SQLiteDb
{
    public class SQLiteDbContext : DbContext
    {
        private readonly ILoggerFactory _loggerFactory;

        public SQLiteDbContext(DbContextOptions<SQLiteDbContext> options, ILoggerFactory loggerFactory)
            : base(options)
        {
            _loggerFactory = loggerFactory;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .UseLoggerFactory(_loggerFactory);
        }

        public DbSet<Track> Track { get; set; }
        public DbSet<SubFingerprint> SubFingerprint { get; set; }
        public DbSet<Hash> Hash { get; set; }

    }
}