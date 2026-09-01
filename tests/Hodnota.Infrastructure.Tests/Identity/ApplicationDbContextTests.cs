using AwesomeAssertions;

using Hodnota.Infrastructure.Identity;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Hodnota.Infrastructure.Tests.Identity;

public class ApplicationDbContextTests
{
    // Identity's own built-in entity set (fixed by the framework, unaffected by e.g. adding ApplicationUser
    // profile fields later) — checked with Contain rather than BeEquivalentTo so this doesn't need updating
    // if the model gains unrelated tables later.
    private static readonly IEnumerable<string?> ExpectedIdentityTableNames =
    [
        "AspNetUsers",
        "AspNetRoles",
        "AspNetUserRoles",
        "AspNetUserClaims",
        "AspNetRoleClaims",
        "AspNetUserLogins",
        "AspNetUserTokens",
    ];

    [Fact]
    public async Task EnsureCreated_BuildsAllExpectedIdentityTables()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var context = new ApplicationDbContext(options);

        await context.Database.EnsureCreatedAsync();

        var tableNames = context.Model.GetEntityTypes().Select(entityType => entityType.GetTableName());
        tableNames.Should().Contain(ExpectedIdentityTableNames);
    }

    [Fact]
    public async Task Users_CanBeInsertedAndQueriedByGuidKey()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var user = new ApplicationUser { UserName = "user@example.com", Email = "user@example.com" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var found = await context.Users.FindAsync(user.Id);
        found.Should().NotBeNull();
        found!.Email.Should().Be("user@example.com");
    }
}
