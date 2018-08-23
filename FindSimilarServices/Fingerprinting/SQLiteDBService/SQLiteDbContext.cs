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
        public SQLiteDbContext(DbContextOptions<SQLiteDbContext> options)
            : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SubFingerprint>()
                .HasIndex(x => new
                {
                    x.TrackId,
                    x.HashTable0,
                    x.HashTable1,
                    x.HashTable2,
                    x.HashTable3,
                    x.HashTable4,
                    x.HashTable5,
                    x.HashTable6,
                    x.HashTable7,
                    x.HashTable8,
                    x.HashTable9,
                    x.HashTable10,
                    x.HashTable11,
                    x.HashTable12,
                    x.HashTable13,
                    x.HashTable14,
                    x.HashTable15,
                    x.HashTable16,
                    x.HashTable17,
                    x.HashTable18,
                    x.HashTable19,
                    x.HashTable20,
                    x.HashTable21,
                    x.HashTable22,
                    x.HashTable23,
                    x.HashTable24
                });

            modelBuilder.Entity<Track>()
                .HasIndex(x => new
                {
                    x.Title
                });
        }

        public DbSet<Track> Track { get; set; }
        public DbSet<SubFingerprint> SubFingerprint { get; set; }

    }
}