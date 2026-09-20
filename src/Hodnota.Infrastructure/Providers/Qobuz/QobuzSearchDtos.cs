using System.Text.Json.Serialization;

namespace Hodnota.Infrastructure.Providers.Qobuz;

public sealed record QobuzSearchResponse(
    [property: JsonPropertyName("tracks")] QobuzPagedResult<QobuzTrack>? Tracks,
    [property: JsonPropertyName("albums")] QobuzPagedResult<QobuzAlbum>? Albums);

public sealed record QobuzPagedResult<T>([property: JsonPropertyName("items")] IReadOnlyList<T>? Items);

public sealed record QobuzTrack(
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("isrc")] string? Isrc,
    [property: JsonPropertyName("performer")] QobuzArtist? Performer,
    [property: JsonPropertyName("album")] QobuzAlbum? Album);

public sealed record QobuzAlbum(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("upc")] string? Upc,
    [property: JsonPropertyName("artist")] QobuzArtist? Artist,
    [property: JsonPropertyName("image")] QobuzImage? Image,
    [property: JsonPropertyName("release_type")] string? ReleaseType);

public sealed record QobuzArtist([property: JsonPropertyName("name")] string? Name);

public sealed record QobuzImage(
    [property: JsonPropertyName("large")] string? Large,
    [property: JsonPropertyName("medium")] string? Medium,
    [property: JsonPropertyName("thumbnail")] string? Thumbnail);
