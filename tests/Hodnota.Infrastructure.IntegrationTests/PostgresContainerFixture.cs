using Testcontainers.PostgreSql;

namespace Hodnota.Infrastructure.IntegrationTests;

// Ephemeral — deliberately separate from the persistent local dev container in docker-compose.yml.
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
