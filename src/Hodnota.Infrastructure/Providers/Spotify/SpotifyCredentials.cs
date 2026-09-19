namespace Hodnota.Infrastructure.Providers.Spotify;

// Market is optional — omitted, the search covers the widest possible catalog with no
// server-side country bias. See ADR 0011.
public sealed record SpotifyCredentials(string ClientId, string ClientSecret, string? Market);
