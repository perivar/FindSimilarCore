using Microsoft.EntityFrameworkCore;
using FindSimilarServices.Fingerprinting.SQLiteDb.Models;

namespace FindSimilarServices.Fingerprinting.SQLiteDb
{
    public class SQLiteDbService : DbContext
    {
        public SQLiteDbService(DbContextOptions<SQLiteDbService> options) : base(options)
        { }

        public DbSet<Track> Tracks { get; set; }
        public DbSet<SubFingerprint> SubFingerprints { get; set; }
        public DbSet<Hash> Hashes { get; set; }

    }
}