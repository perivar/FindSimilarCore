using Microsoft.EntityFrameworkCore;
using FindSimilarServices.Fingerprinting.SQLiteDb.Models;

namespace FindSimilarServices.Fingerprinting.SQLiteDb
{
    public class SQLiteDbContext : DbContext
    {
        public SQLiteDbContext(DbContextOptions<SQLiteDbContext> options) 
            : base(options)
        { }

        public DbSet<Track> Track { get; set; }
        public DbSet<SubFingerprint> SubFingerprint { get; set; }
        public DbSet<Hash> Hash { get; set; }

    }
}