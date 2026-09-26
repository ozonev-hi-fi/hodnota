using System.Text.Json.Serialization;

namespace Hodnota.Infrastructure.Providers.Discogs;

public sealed record DiscogsSearchResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<DiscogsSearchResult>? Results);

public sealed record DiscogsSearchResult(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("format")] IReadOnlyList<string>? Format,
    [property: JsonPropertyName("master_id")] long? MasterId,
    [property: JsonPropertyName("cover_image")] string? CoverImage);
