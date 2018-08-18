using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FindSimilarServices.Fingerprinting.SQLiteDb
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SQLiteDbService>
    {
        public SQLiteDbService CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<SQLiteDbService>();

            builder.UseSqlite("Data Source=findsimilar.db");

            return new SQLiteDbService(builder.Options);
        }
    }
}