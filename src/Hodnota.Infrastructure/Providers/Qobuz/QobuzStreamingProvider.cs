using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;

namespace Hodnota.Infrastructure.Providers.Qobuz;

public sealed class QobuzStreamingProvider(QobuzApiClient apiClient) : IStreamingProvider
{
    private const string UnknownArtist = "Unknown";

    public string ProviderCode => ProviderCodes.Qobuz;

    public async Task<IReadOnlyList<StreamingSearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var response = await apiClient.SearchAsync(query, cancellationToken);

        var tracks = (response.Tracks?.Items ?? []).Where(IsUsable).Select(ToSearchResult);
        var albums = (response.Albums?.Items ?? []).Where(IsUsable).Select(ToSearchResult);

        return [.. tracks, .. albums];
    }

    internal static bool IsUsable(QobuzTrack track) =>
        track.Id.HasValue && !string.IsNullOrWhiteSpace(track.Title);

    internal static bool IsUsable(QobuzAlbum album) =>
        !string.IsNullOrWhiteSpace(album.Id) && !string.IsNullOrWhiteSpace(album.Title);

    internal static StreamingSearchResult ToSearchResult(QobuzTrack track)
    {
        var externalId = track.Id!.Value.ToString();

        return new StreamingSearchResult(
            StreamingResultType.Track,
            track.Title!,
            track.Performer?.Name is { Length: > 0 } performer ? performer : UnknownArtist,
            PickImage(track.Album?.Image),
            [new ProviderLinkCandidate(PlatformCodes.Qobuz, externalId, new Uri($"https://open.qobuz.com/track/{externalId}"))],
            Isrc: track.Isrc);
    }

    internal static StreamingSearchResult ToSearchResult(QobuzAlbum album)
    {
        var externalId = album.Id!;

        return new StreamingSearchResult(
            StreamingResultType.Release,
            album.Title!,
            album.Artist?.Name is { Length: > 0 } artist ? artist : UnknownArtist,
            PickImage(album.Image),
            [new ProviderLinkCandidate(PlatformCodes.Qobuz, externalId, BuildAlbumUrl(externalId))],
            ToReleaseType(album.ReleaseType),
            Upc: album.Upc);
    }

    internal static Uri? PickImage(QobuzImage? image)
    {
        var url = image?.Large is { Length: > 0 } large ? large
            : image?.Medium is { Length: > 0 } medium ? medium
            : image?.Thumbnail is { Length: > 0 } thumbnail ? thumbnail
            : null;

        return url is null ? null : new Uri(url);
    }

    internal static ReleaseType ToReleaseType(string? releaseType) => releaseType switch
    {
        "single" => ReleaseType.Single,
        "compilation" => ReleaseType.Compilation,
        "ep" => ReleaseType.EP,
        "live" => ReleaseType.Live,
        _ => ReleaseType.Album,
    };

    private static Uri BuildAlbumUrl(string externalId) => new($"https://open.qobuz.com/album/{externalId}");
}
