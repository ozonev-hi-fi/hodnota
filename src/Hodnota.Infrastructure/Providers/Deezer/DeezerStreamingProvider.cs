using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;

namespace Hodnota.Infrastructure.Providers.Deezer;

public sealed class DeezerStreamingProvider(DeezerApiClient apiClient) : IStreamingProvider
{
    private const string UnknownArtist = "Unknown";

    public string ProviderCode => ProviderCodes.Deezer;

    public IReadOnlyList<string> LinkPlatformCodes { get; } = [PlatformCodes.Deezer];

    public bool Supports(StreamingResultType type) => true;

    public bool SupportsLookup(StreamingResultType type) => true;

    public async Task<IReadOnlyList<StreamingSearchResult>> SearchAsync(string query, StreamingResultType type, CancellationToken cancellationToken)
    {
        if (type == StreamingResultType.Track)
        {
            var tracks = await apiClient.SearchTracksAsync(query, cancellationToken);
            return [.. tracks.Where(IsUsable).Select(ToSearchResult)];
        }

        var albums = await apiClient.SearchAlbumsAsync(query, cancellationToken);
        return [.. albums.Where(IsUsable).Select(ToSearchResult)];
    }

    public async Task<IReadOnlyList<StreamingSearchResult>> LookupAsync(StreamingLookupKey key, CancellationToken cancellationToken)
    {
        if (key.Type == StreamingResultType.Track)
        {
            var track = await apiClient.GetTrackByIsrcAsync(key.Codes[0], cancellationToken);
            return track is not null && IsUsable(track) && CatalogKeys.NormalizeIsrc(track.Isrc) is { } isrc && key.Codes.Contains(isrc)
                ? [ToSearchResult(track)]
                : [];
        }

        foreach (var code in key.Codes)
        {
            var album = await apiClient.GetAlbumByUpcAsync(code, cancellationToken);
            if (album is not null && IsUsable(album) && CatalogKeys.BarcodeVariants(album.Upc).Any(key.Codes.Contains))
            {
                return [ToSearchResult(album)];
            }
        }

        return [];
    }

    internal static bool IsUsable(DeezerTrack track) => track.Id.HasValue && !string.IsNullOrWhiteSpace(track.Title);

    internal static bool IsUsable(DeezerAlbum album) => album.Id.HasValue && !string.IsNullOrWhiteSpace(album.Title);

    internal static StreamingSearchResult ToSearchResult(DeezerTrack track)
    {
        var externalId = track.Id!.Value.ToString();

        return new StreamingSearchResult(
            StreamingResultType.Track,
            track.Title!,
            track.Artist?.Name is { Length: > 0 } artist ? artist : UnknownArtist,
            PickImage(track.Album),
            [new ProviderLinkCandidate(PlatformCodes.Deezer, externalId, BuildUrl(track.Link, "track", externalId))],
            Isrc: track.Isrc);
    }

    internal static StreamingSearchResult ToSearchResult(DeezerAlbum album)
    {
        var externalId = album.Id!.Value.ToString();

        return new StreamingSearchResult(
            StreamingResultType.Release,
            album.Title!,
            album.Artist?.Name is { Length: > 0 } artist ? artist : UnknownArtist,
            PickImage(album),
            [new ProviderLinkCandidate(PlatformCodes.Deezer, externalId, BuildUrl(album.Link, "album", externalId))],
            ToReleaseType(album.RecordType),
            Upc: album.Upc);
    }

    internal static Uri? PickImage(DeezerAlbum? album)
    {
        var url = album?.CoverBig is { Length: > 0 } big ? big
            : album?.CoverXl is { Length: > 0 } xl ? xl
            : album?.CoverMedium is { Length: > 0 } medium ? medium
            : null;

        return url is not null && Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null;
    }

    internal static ReleaseType ToReleaseType(string? recordType) => recordType switch
    {
        "single" => ReleaseType.Single,
        "ep" => ReleaseType.EP,
        "compile" => ReleaseType.Compilation,
        _ => ReleaseType.Album,
    };

    internal static Uri BuildUrl(string? link, string entityKind, string externalId) =>
        Uri.TryCreate(link, UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
            ? url
            : new Uri($"https://www.deezer.com/{entityKind}/{externalId}");
}
