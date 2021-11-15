namespace EnoDatabase;

public class EnoDbContextFactory : IDesignTimeDbContextFactory<EnoDbContext>
{
    public EnoDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EnoDbContext>();
        optionsBuilder.UseNpgsql(EnoDbContext.PostgresConnectionString, pgoptions => pgoptions.EnableRetryOnFailure());
        return new EnoDbContext(optionsBuilder.Options);
    }
}
