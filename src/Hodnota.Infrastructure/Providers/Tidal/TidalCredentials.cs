namespace Hodnota.Infrastructure.Providers.Tidal;

// CountryCode is optional — omitted, Tidal still answers a search. See ADR 0013.
public sealed record TidalCredentials(string ClientId, string ClientSecret, string? CountryCode);
