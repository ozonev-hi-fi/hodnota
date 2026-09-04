using AwesomeAssertions;

using Hodnota.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hodnota.Infrastructure.IntegrationTests;

// Catches SQLite/Postgres translation divergences the fast SQLite-backed test suites can't see.
public class PostgresIdentitySchemaTests(PostgresContainerFixture fixture) : IClassFixture<PostgresContainerFixture>
{
    [Fact]
    public async Task Migrate_AppliesInitialCreate()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.ConnectionString).Options;
        await using var context = new ApplicationDbContext(options);

        await context.Database.MigrateAsync();

        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(migrationId => migrationId.Contains("InitialCreate"));
    }

    [Fact]
    public async Task UserManagerAndRoleManager_RoundTripAgainstRealPostgres()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
        services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var email = $"{Guid.NewGuid():N}@example.com";

        var createResult = await userManager.CreateAsync(new ApplicationUser { UserName = email, Email = email }, "P@ssw0rd!123");
        createResult.Succeeded.Should().BeTrue();

        var found = await userManager.FindByEmailAsync(email);
        found.Should().NotBeNull();
        (await userManager.CheckPasswordAsync(found!, "P@ssw0rd!123")).Should().BeTrue();

        var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>("Member"));
        roleResult.Succeeded.Should().BeTrue();
    }
}
