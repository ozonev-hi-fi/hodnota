namespace Hodnota.Infrastructure.Providers.Deezer;

public static class DeezerConfiguration
{
    // No app id or token: Deezer's catalog endpoints are public, see ADR 0015.
    public const string ApiHttpClientName = "Deezer.Api";
}
