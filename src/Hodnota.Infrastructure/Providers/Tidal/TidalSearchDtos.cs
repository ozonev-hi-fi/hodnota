using System.Text.Json.Serialization;

namespace Hodnota.Infrastructure.Providers.Tidal;

public sealed record TidalTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

public sealed record TidalSearchDocument(
    [property: JsonPropertyName("data")] IReadOnlyList<TidalResource>? Data,
    [property: JsonPropertyName("included")] IReadOnlyList<TidalResource>? Included);

// One shape for every JSON:API resource type (searchResults, tracks, albums, artists, artworks):
// each type fills only its own attributes and relationships, the rest stay null.
public sealed record TidalResource(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("attributes")] TidalAttributes? Attributes,
    [property: JsonPropertyName("relationships")] IReadOnlyDictionary<string, TidalRelationship>? Relationships);

public sealed record TidalRelationship([property: JsonPropertyName("data")] IReadOnlyList<TidalResourceReference>? Data);

public sealed record TidalResourceReference(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("type")] string? Type);

public sealed record TidalAttributes(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("isrc")] string? Isrc,
    [property: JsonPropertyName("barcodeId")] string? BarcodeId,
    [property: JsonPropertyName("albumType")] string? AlbumType,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("externalLinks")] IReadOnlyList<TidalExternalLink>? ExternalLinks,
    [property: JsonPropertyName("files")] IReadOnlyList<TidalFile>? Files);

public sealed record TidalExternalLink(
    [property: JsonPropertyName("href")] string? Href,
    [property: JsonPropertyName("meta")] TidalLinkMeta? Meta);

public sealed record TidalLinkMeta([property: JsonPropertyName("type")] string? Type);

public sealed record TidalFile(
    [property: JsonPropertyName("href")] string? Href,
    [property: JsonPropertyName("meta")] TidalFileMeta? Meta);

public sealed record TidalFileMeta(
    [property: JsonPropertyName("width")] int? Width,
    [property: JsonPropertyName("height")] int? Height);
