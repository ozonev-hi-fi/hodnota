using System.Text.Json.Serialization;

namespace Hodnota.Infrastructure.Providers.AppleMusic;

public sealed record ITunesSearchResponse(
    [property: JsonPropertyName("resultCount")] int? ResultCount,
    [property: JsonPropertyName("results")] IReadOnlyList<ITunesItem>? Results);

public sealed record ITunesItem(
    [property: JsonPropertyName("wrapperType")] string? WrapperType,
    [property: JsonPropertyName("kind")] string? Kind,
    [property: JsonPropertyName("trackId")] long? TrackId,
    [property: JsonPropertyName("collectionId")] long? CollectionId,
    [property: JsonPropertyName("artistName")] string? ArtistName,
    [property: JsonPropertyName("trackName")] string? TrackName,
    [property: JsonPropertyName("collectionName")] string? CollectionName,
    [property: JsonPropertyName("collectionType")] string? CollectionType,
    [property: JsonPropertyName("trackViewUrl")] string? TrackViewUrl,
    [property: JsonPropertyName("collectionViewUrl")] string? CollectionViewUrl,
    [property: JsonPropertyName("artworkUrl100")] string? ArtworkUrl100,
    [property: JsonPropertyName("isStreamable")] bool? IsStreamable);
