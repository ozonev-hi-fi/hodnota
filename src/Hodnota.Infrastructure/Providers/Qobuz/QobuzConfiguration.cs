namespace Hodnota.Infrastructure.Providers.Qobuz;

public static class QobuzConfiguration
{
    public const string AppIdConfigKey = "Qobuz:AppId";

    public const string UserTokenConfigKey = "Qobuz:UserToken";

    public const string ApiHttpClientName = "Qobuz.Api";

    // Qobuz's undocumented API is known to reject requests with no browser-like User-Agent —
    // matches what the ecosystem's own client tooling sends. Spotify/YouTube need no equivalent.
    public const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36";
}
