namespace Hodnota.Infrastructure;

// Reused everywhere instead of retyped literals — see architecture.md's Tooling & Conventions section.
public static class DatabaseConfiguration
{
    public const string ProviderConfigKey = "Database:Provider";
    public const string ConnectionStringName = "Default";
    public const string PostgresProviderName = "Postgres";
    public const string SqliteProviderName = "Sqlite";

    public static bool IsPostgres(string? provider) => provider == PostgresProviderName;
}
