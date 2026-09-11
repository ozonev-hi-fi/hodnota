using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hodnota.Infrastructure;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        DotEnvLoader.LoadIfPresent();

        var connectionString = Environment.GetEnvironmentVariable($"ConnectionStrings__{DatabaseConfiguration.ConnectionStringName}");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                $"Missing 'ConnectionStrings__{DatabaseConfiguration.ConnectionStringName}' — ensure the repo-root .env file exists, or set the environment variable manually.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
