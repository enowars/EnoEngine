namespace EnoDatabase
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Design;

    public class EnoDatabaseContextFactory : IDesignTimeDbContextFactory<EnoDatabaseContext>
    {
        public EnoDatabaseContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<EnoDatabaseContext>();
            optionsBuilder.UseNpgsql(EnoDatabaseContext.PostgresConnectionString, pgoptions => pgoptions.EnableRetryOnFailure());
            return new EnoDatabaseContext(optionsBuilder.Options);
        }
    }
}
