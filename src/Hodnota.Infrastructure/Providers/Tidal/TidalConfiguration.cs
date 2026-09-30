namespace Hodnota.Infrastructure.Providers.Tidal;

public static class TidalConfiguration
{
    public const string ClientIdConfigKey = "Tidal:ClientId";
    public const string ClientSecretConfigKey = "Tidal:ClientSecret";
    public const string CountryCodeConfigKey = "Tidal:CountryCode";

    public const string AuthHttpClientName = "Tidal.Auth";
    public const string ApiHttpClientName = "Tidal.Api";
}
