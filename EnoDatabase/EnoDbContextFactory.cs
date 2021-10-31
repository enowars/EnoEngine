namespace EnoDatabase
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Design;

    public class EnoDbContextFactory : IDesignTimeDbContextFactory<EnoDbContext>
    {
        public EnoDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<EnoDbContext>();
            optionsBuilder.UseNpgsql(EnoDbContext.PostgresConnectionString, pgoptions => pgoptions.EnableRetryOnFailure());
            return new EnoDbContext(optionsBuilder.Options);
        }
    }
}
