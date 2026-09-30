using Google.Apis.YouTube.v3;

using Hodnota.Api.OpenApi;
using Hodnota.Infrastructure;
using Hodnota.Infrastructure.Identity;
using Hodnota.Infrastructure.Providers.Discogs;
using Hodnota.Infrastructure.Providers.Tidal;

using Microsoft.EntityFrameworkCore;

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
