using Google.Apis.YouTube.v3;

using Hodnota.Api.OpenApi;
using Hodnota.Infrastructure;
using Hodnota.Infrastructure.Identity;
using Hodnota.Infrastructure.Providers.Discogs;
using Hodnota.Infrastructure.Providers.Tidal;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using Scalar.AspNetCore;

DotEnvLoader.LoadIfPresent();

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddInfrastructure(builder.Configuration)
    .AddDocumentation()
    .AddControllers();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (DatabaseConfiguration.IsPostgres(app.Configuration[DatabaseConfiguration.ProviderConfigKey]))
    {
        await dbContext.Database.MigrateAsync();
    }
    else
    {
        await dbContext.Database.EnsureCreatedAsync();
    }

    // Forces the YouTubeService, DiscogsCredentials and TidalCredentials singleton factories to run now. Failing fast on a missing/invalid YouTube:ApiKey, Discogs:Token or Tidal:ClientId/ClientSecret at startup.
    _ = scope.ServiceProvider.GetRequiredService<YouTubeService>();
    _ = scope.ServiceProvider.GetRequiredService<DiscogsCredentials>();
    _ = scope.ServiceProvider.GetRequiredService<TidalCredentials>();

    await SeedAdminRoleAsync(scope.ServiceProvider, app.Configuration);
}

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGroup("/api/auth").MapIdentityApi<ApplicationUser>();
app.MapControllers();

await app.RunAsync();

static async Task SeedAdminRoleAsync(IServiceProvider services, IConfiguration configuration)
{
    var adminEmail = configuration[AdminConfiguration.EmailConfigKey];
    if (string.IsNullOrEmpty(adminEmail))
    {
        return;
    }

    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var user = await userManager.FindByEmailAsync(adminEmail);
    if (user is null)
    {
        return;
    }

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    if (!await roleManager.RoleExistsAsync(AdminConfiguration.RoleName))
    {
        ThrowIfFailed(await roleManager.CreateAsync(new IdentityRole<Guid>(AdminConfiguration.RoleName)), $"Creating the '{AdminConfiguration.RoleName}' role failed");
    }

    if (!await userManager.IsInRoleAsync(user, AdminConfiguration.RoleName))
    {
        ThrowIfFailed(await userManager.AddToRoleAsync(user, AdminConfiguration.RoleName), $"Assigning the '{AdminConfiguration.RoleName}' role to {adminEmail} failed");
    }
}

// Identity reports these failures as a result, not an exception — unchecked, startup would carry on
// and the admin would only ever see a 403.
static void ThrowIfFailed(IdentityResult result, string what)
{
    if (!result.Succeeded)
    {
        throw new InvalidOperationException($"{what}: {string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"))}");
    }
}
