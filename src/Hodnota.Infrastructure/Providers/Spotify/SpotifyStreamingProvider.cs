using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;

namespace Hodnota.Infrastructure.Providers.Spotify;

public sealed class SpotifyStreamingProvider(SpotifyApiClient apiClient) : IStreamingProvider
{
    // Matches YouTube's Thumbnails.Medium (320x180) so a merged row's image looks consistent
    // whichever provider ends up owning it.
    private const int MinimumPreferredImageWidth = 300;
    private const string UnknownArtist = "Unknown";

    public string ProviderCode => ProviderCodes.Spotify;

    public async Task<IReadOnlyList<StreamingSearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var response = await apiClient.SearchAsync(query, cancellationToken);

        var tracks = (response.Tracks?.Items ?? []).Where(IsUsable).Select(ToSearchResult);
        var albums = (response.Albums?.Items ?? []).Where(IsUsable).Select(ToSearchResult);

        return [.. tracks, .. albums];
    }

    internal static bool IsUsable(SpotifyTrack track) =>
        !string.IsNullOrWhiteSpace(track.Id) && !string.IsNullOrWhiteSpace(track.Name);

    internal static bool IsUsable(SpotifyAlbum album) =>
        !string.IsNullOrWhiteSpace(album.Id) && !string.IsNullOrWhiteSpace(album.Name);

    internal static StreamingSearchResult ToSearchResult(SpotifyTrack track)
    {
        var externalId = track.Id!;

        return new StreamingSearchResult(
            StreamingResultType.Track,
            track.Name!,
            JoinArtists(track.Artists),
            PickImage(track.Album?.Images),
            [new ProviderLinkCandidate(PlatformCodes.Spotify, externalId, BuildUrl(track.ExternalUrls, "track", externalId))],
            Isrc: track.ExternalIds?.Isrc);
    }

    internal static StreamingSearchResult ToSearchResult(SpotifyAlbum album)
    {
        var externalId = album.Id!;

        return new StreamingSearchResult(
            StreamingResultType.Release,
            album.Name!,
            JoinArtists(album.Artists),
            PickImage(album.Images),
            [new ProviderLinkCandidate(PlatformCodes.Spotify, externalId, BuildUrl(album.ExternalUrls, "album", externalId))],
            ToReleaseType(album.AlbumType));
    }

    internal static string JoinArtists(IReadOnlyList<SpotifyArtist>? artists)
    {
        var names = (artists ?? []).Select(artist => artist.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
        return names.Count == 0 ? UnknownArtist : string.Join(", ", names);
    }

    internal static Uri? PickImage(IReadOnlyList<SpotifyImage>? images)
    {
        if (images is null or { Count: 0 })
        {
            return null;
        }

        var atLeastPreferred = images
            .Where(image => image.Width >= MinimumPreferredImageWidth)
            .OrderBy(image => image.Width)
            .FirstOrDefault();

        var chosen = atLeastPreferred ?? images.OrderByDescending(image => image.Width ?? 0).First();
        return new Uri(chosen.Url);
    }

    internal static ReleaseType ToReleaseType(string? albumType) => albumType switch
    {
        "single" => ReleaseType.Single,
        "compilation" => ReleaseType.Compilation,
        _ => ReleaseType.Album,
    };

    private static Uri BuildUrl(SpotifyExternalUrls? externalUrls, string entityKind, string externalId) =>
        externalUrls?.Spotify is { Length: > 0 } url
            ? new Uri(url)
            : new Uri($"https://open.spotify.com/{entityKind}/{externalId}");
}
