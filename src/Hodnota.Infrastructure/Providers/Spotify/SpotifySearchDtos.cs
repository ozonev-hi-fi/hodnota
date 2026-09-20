using System.Text.Json.Serialization;

namespace Hodnota.Infrastructure.Providers.Spotify;

public sealed record SpotifyTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

public sealed record SpotifySearchResponse(
    [property: JsonPropertyName("tracks")] SpotifyPagedResult<SpotifyTrack>? Tracks,
    [property: JsonPropertyName("albums")] SpotifyPagedResult<SpotifyAlbum>? Albums);

public sealed record SpotifyPagedResult<T>([property: JsonPropertyName("items")] IReadOnlyList<T>? Items);

public sealed record SpotifyTrack(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("artists")] IReadOnlyList<SpotifyArtist>? Artists,
    [property: JsonPropertyName("album")] SpotifyAlbum? Album,
    [property: JsonPropertyName("external_urls")] SpotifyExternalUrls? ExternalUrls,
    [property: JsonPropertyName("external_ids")] SpotifyExternalIds? ExternalIds = null);

public sealed record SpotifyAlbum(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("album_type")] string? AlbumType,
    [property: JsonPropertyName("artists")] IReadOnlyList<SpotifyArtist>? Artists,
    [property: JsonPropertyName("images")] IReadOnlyList<SpotifyImage>? Images,
    [property: JsonPropertyName("external_urls")] SpotifyExternalUrls? ExternalUrls);

public sealed record SpotifyArtist([property: JsonPropertyName("name")] string? Name);

public sealed record SpotifyImage(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("width")] int? Width,
    [property: JsonPropertyName("height")] int? Height);

public sealed record SpotifyExternalUrls([property: JsonPropertyName("spotify")] string? Spotify);

public sealed record SpotifyExternalIds([property: JsonPropertyName("isrc")] string? Isrc);
