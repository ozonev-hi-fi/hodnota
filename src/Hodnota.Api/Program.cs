using Hodnota.Api.OpenApi;
using Hodnota.Infrastructure;
using Hodnota.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;

using Scalar.AspNetCore;

DotEnvLoader.LoadIfPresent();

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddInfrastructure()
    .AddDocumentation();

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
}

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGroup("/api/auth").MapIdentityApi<ApplicationUser>();

await app.RunAsync();
