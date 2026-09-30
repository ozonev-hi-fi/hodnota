using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;

using IncludedIndex = System.Collections.Generic.IReadOnlyDictionary<(string Type, string Id), Hodnota.Infrastructure.Providers.Tidal.TidalResource>;

namespace Hodnota.Infrastructure.Providers.Tidal;

public sealed class TidalStreamingProvider(TidalApiClient apiClient) : IStreamingProvider
{
    // Tidal returns 20 results per type and ignores a page-size parameter, so the cut happens here.
    private const int MaxResults = 5;

    // Matches YouTube's Thumbnails.Medium (320x180) so a merged row's image looks consistent
    // whichever provider ends up owning it.
    private const int MinimumPreferredImageWidth = 300;
    private const string UnknownArtist = "Unknown";
    private const string SharingLinkType = "TIDAL_SHARING";

    public string ProviderCode => ProviderCodes.Tidal;

    public bool Supports(StreamingResultType type) => true;

    public async Task<IReadOnlyList<StreamingSearchResult>> SearchAsync(string query, StreamingResultType type, CancellationToken cancellationToken)
    {
        var document = await apiClient.SearchAsync(query, type, cancellationToken);
        var included = IndexIncluded(document.Included);

        var isTrack = type == StreamingResultType.Track;
        var references = document.Data?.FirstOrDefault()?.Relationships?.GetValueOrDefault(isTrack ? "tracks" : "albums")?.Data ?? [];

        return
        [
            .. references
                .Select(reference => Find(included, reference))
                .OfType<TidalResource>()
                .Where(IsUsable)
                .Take(MaxResults)
                .Select(resource => isTrack ? ToTrackResult(resource, included) : ToAlbumResult(resource, included)),
        ];
    }

    internal static IncludedIndex IndexIncluded(IReadOnlyList<TidalResource>? included)
    {
        var index = new Dictionary<(string Type, string Id), TidalResource>();
        foreach (var resource in included ?? [])
        {
            if (resource.Type is not null && resource.Id is not null)
            {
                index[(resource.Type, resource.Id)] = resource;
            }
        }

        return index;
    }

    internal static bool IsUsable(TidalResource resource) =>
        !string.IsNullOrWhiteSpace(resource.Id) && !string.IsNullOrWhiteSpace(resource.Attributes?.Title);

    internal static StreamingSearchResult ToTrackResult(TidalResource track, IncludedIndex included)
    {
        var externalId = track.Id!;
        var attributes = track.Attributes!;
        var album = Related(track, "albums", included).FirstOrDefault();

        return new StreamingSearchResult(
            StreamingResultType.Track,
            BuildTrackName(attributes.Title!, attributes.Version),
            JoinArtists(Related(track, "artists", included)),
            album is null ? null : PickImage(Related(album, "coverArt", included).FirstOrDefault()?.Attributes?.Files),
            [new ProviderLinkCandidate(PlatformCodes.Tidal, externalId, BuildUrl(attributes.ExternalLinks, "track", externalId))],
            Isrc: attributes.Isrc);
    }

    internal static StreamingSearchResult ToAlbumResult(TidalResource album, IncludedIndex included)
    {
        var externalId = album.Id!;
        var attributes = album.Attributes!;

        return new StreamingSearchResult(
            StreamingResultType.Release,
            attributes.Title!,
            JoinArtists(Related(album, "artists", included)),
            PickImage(Related(album, "coverArt", included).FirstOrDefault()?.Attributes?.Files),
            [new ProviderLinkCandidate(PlatformCodes.Tidal, externalId, BuildUrl(attributes.ExternalLinks, "album", externalId))],
            ToReleaseType(attributes.AlbumType),
            Upc: attributes.BarcodeId);
    }

    // Tidal's version field is free text ("Live", "Bonus Track", ...), null or empty for most tracks.
    internal static string BuildTrackName(string title, string? version) =>
        string.IsNullOrWhiteSpace(version) ? title : $"{title} ({version})";

    internal static string JoinArtists(IEnumerable<TidalResource> artists)
    {
        var names = artists.Select(artist => artist.Attributes?.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
        return names.Count == 0 ? UnknownArtist : string.Join(", ", names);
    }

    internal static Uri? PickImage(IReadOnlyList<TidalFile>? files)
    {
        var usable = (files ?? [])
            .Where(file => Uri.TryCreate(file.Href, UriKind.Absolute, out _))
            .ToList();
        if (usable.Count == 0)
        {
            return null;
        }

        var atLeastPreferred = usable
            .Where(file => file.Meta?.Width >= MinimumPreferredImageWidth)
            .OrderBy(file => file.Meta!.Width)
            .FirstOrDefault();

        var chosen = atLeastPreferred ?? usable.OrderByDescending(file => file.Meta?.Width ?? 0).First();
        return new Uri(chosen.Href!);
    }

    internal static ReleaseType ToReleaseType(string? albumType) => albumType?.ToUpperInvariant() switch
    {
        "SINGLE" => ReleaseType.Single,
        "EP" => ReleaseType.EP,
        _ => ReleaseType.Album,
    };

    internal static Uri BuildUrl(IReadOnlyList<TidalExternalLink>? externalLinks, string entityKind, string externalId)
    {
        foreach (var link in externalLinks ?? [])
        {
            if (link.Meta?.Type == SharingLinkType
                && Uri.TryCreate(link.Href, UriKind.Absolute, out var url)
                && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps))
            {
                return url;
            }
        }

        return new Uri($"https://tidal.com/browse/{entityKind}/{externalId}");
    }

    private static TidalResource? Find(IncludedIndex included, TidalResourceReference reference) =>
        reference.Type is not null && reference.Id is not null && included.TryGetValue((reference.Type, reference.Id), out var resource)
            ? resource
            : null;

    private static IEnumerable<TidalResource> Related(TidalResource resource, string relationship, IncludedIndex included) =>
        (resource.Relationships?.GetValueOrDefault(relationship)?.Data ?? [])
            .Select(reference => Find(included, reference))
            .OfType<TidalResource>();
}
