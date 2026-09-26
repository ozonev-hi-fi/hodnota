namespace Hodnota.Infrastructure.Providers.Discogs;

public static class DiscogsConfiguration
{
    public const string TokenConfigKey = "Discogs:Token";

    public const string ApiHttpClientName = "Discogs.Api";

    // HttpHeaderValueCollection<ProductInfoHeaderValue>.ParseAdd requires a bare URL to be wrapped
    // in parentheses as a "comment" token — an unparenthesized "+https://..." throws FormatException.
    public const string UserAgent = "hodnota/1.0 (+https://github.com/ozonev-hi-fi/hodnota)";
}
