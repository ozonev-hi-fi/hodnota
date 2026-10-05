using System.Text.Json.Serialization;

namespace Hodnota.Infrastructure.Providers.Deezer;

public sealed record DeezerError(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("code")] int? Code);

public sealed record DeezerPage<T>(
    [property: JsonPropertyName("data")] IReadOnlyList<T>? Data,
    [property: JsonPropertyName("error")] DeezerError? Error);

public sealed record DeezerTrack(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("isrc")] string? Isrc,
    [property: JsonPropertyName("link")] string? Link,
    [property: JsonPropertyName("artist")] DeezerArtist? Artist,
    [property: JsonPropertyName("album")] DeezerAlbum? Album,
    [property: JsonPropertyName("error")] DeezerError? Error = null);

public sealed record DeezerAlbum(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("upc")] string? Upc,
    [property: JsonPropertyName("link")] string? Link,
    [property: JsonPropertyName("record_type")] string? RecordType,
    [property: JsonPropertyName("cover_medium")] string? CoverMedium,
    [property: JsonPropertyName("cover_big")] string? CoverBig,
    [property: JsonPropertyName("cover_xl")] string? CoverXl,
    [property: JsonPropertyName("artist")] DeezerArtist? Artist,
    [property: JsonPropertyName("error")] DeezerError? Error = null);

public sealed record DeezerArtist([property: JsonPropertyName("name")] string? Name);
