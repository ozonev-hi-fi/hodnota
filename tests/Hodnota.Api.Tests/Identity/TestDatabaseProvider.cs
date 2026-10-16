using Hodnota.Infrastructure;

namespace Hodnota.Api.Tests.Identity;

// For every WebApplicationFactory<Program> host: DependencyInjection picks the Quartz job store from
// Database:Provider before Build(), when ConfigureAppConfiguration has not applied yet. A real
// environment variable is visible at once, and set before .env loads (NoClobber) it also beats a
// developer's own Database__Provider=Postgres there. Every test host wants Sqlite, so concurrent
// test classes setting the same process-wide value is harmless.
internal static class TestDatabaseProvider
{
    public static void UseSqlite() =>
        Environment.SetEnvironmentVariable("Database__Provider", DatabaseConfiguration.SqliteProviderName);
}
