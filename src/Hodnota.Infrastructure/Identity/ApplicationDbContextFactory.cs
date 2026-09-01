using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hodnota.Infrastructure.Identity;

// Needed because ApplicationDbContext lives in a class library with no host to supply options at design time.
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        DotEnvLoader.LoadIfPresent();

        // Reads the environment variable directly, not via IConfiguration/appsettings.json — dotnet-ef
        // tooling only ever gets its connection string from .env/the environment, by design (see
        // docs/decisions/0005-auth-identity.md). Setting ConnectionStrings:Default in an appsettings.*.json
        // has no effect here.
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
