using System.Text.RegularExpressions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;

namespace Hodnota.Infrastructure.Providers.Discogs;

public sealed partial class DiscogsStreamingProvider(DiscogsApiClient apiClient) : IStreamingProvider
{
    private const int MaxResults = 5;
    private const string UnknownArtist = "Unknown";
    private const string TitleSeparator = " - ";
    private const string MasterResultType = "master";
    private const string ReleaseResultType = "release";
    private const string PlaceholderImageFileName = "spacer.gif";

    public string ProviderCode => ProviderCodes.Discogs;

    public IReadOnlyList<string> LinkPlatformCodes { get; } = [PlatformCodes.Discogs];

    public bool Supports(StreamingResultType type) => type == StreamingResultType.Release;

    public bool SupportsLookup(StreamingResultType type) => type == StreamingResultType.Release;

    public async Task<IReadOnlyList<StreamingSearchResult>> LookupAsync(StreamingLookupKey key, CancellationToken cancellationToken)
    {
        if (key.Type != StreamingResultType.Release)
        {
            return [];
        }

        foreach (var code in key.Codes)
        {
            var response = await apiClient.LookupByBarcodeAsync(code, cancellationToken);

            // Masters first: a master stands for every pressing of the album.
            List<StreamingSearchResult> matches =
            [
                .. (response.Results ?? [])
                    .Where(result => result.Id.HasValue && !string.IsNullOrWhiteSpace(result.Title) && result.Type == ReleaseResultType)
                    .Where(result => HasBarcode(result, key))
                    .OrderBy(result => HasMaster(result) ? 0 : 1)
                    .Take(MaxResults)
                    .Select(ToLookupResult),
            ];
            if (matches.Count > 0)
            {
                return matches;
            }
        }

        return [];
    }

    // Barcodes are typed in as printed, with spaces and dashes, next to non-barcode text.
    internal static bool HasBarcode(DiscogsSearchResult result, StreamingLookupKey key) =>
        (result.Barcode ?? []).Any(barcode => CatalogKeys.BarcodeVariants(barcode).Any(key.Codes.Contains));

    internal static bool HasMaster(DiscogsSearchResult result) => result.MasterId is > 0;

    internal static StreamingSearchResult ToLookupResult(DiscogsSearchResult result)
    {
        if (!HasMaster(result))
        {
            return ToSearchResult(result);
        }

        var masterId = result.MasterId!.Value;
        var (artistName, name) = SplitTitle(result.Title!);

        return new StreamingSearchResult(
            StreamingResultType.Release,
            name,
            artistName,
            PickImage(result.CoverImage),
            [new ProviderLinkCandidate(PlatformCodes.Discogs, $"{MasterResultType}:{masterId}", new Uri($"https://www.discogs.com/{MasterResultType}/{masterId}"))],
            ReleaseType.Album);
    }

    public async Task<IReadOnlyList<StreamingSearchResult>> SearchAsync(string query, StreamingResultType type, CancellationToken cancellationToken)
    {
        if (type != StreamingResultType.Release)
        {
            return [];
        }

        var masters = (await apiClient.SearchMastersAsync(query, cancellationToken)).Results ?? [];
        if (masters.Count(IsUsable) >= MaxResults)
        {
            return Combine(masters, null);
        }

        var releases = (await apiClient.SearchReleasesAsync(query, cancellationToken)).Results;
        return Combine(masters, releases);
    }

    internal static IReadOnlyList<StreamingSearchResult> Combine(IReadOnlyList<DiscogsSearchResult>? masters, IReadOnlyList<DiscogsSearchResult>? releases) =>
        [.. (masters ?? []).Concat(releases ?? []).Where(IsUsable).Take(MaxResults).Select(ToSearchResult)];

    internal static bool IsUsable(DiscogsSearchResult result) =>
        result.Id.HasValue
        && !string.IsNullOrWhiteSpace(result.Title)
        && (result.Type == MasterResultType || (result.Type == ReleaseResultType && result.MasterId is null or 0));

    internal static StreamingSearchResult ToSearchResult(DiscogsSearchResult result)
    {
        var isMaster = result.Type == MasterResultType;
        var entityType = isMaster ? MasterResultType : ReleaseResultType;
        var id = result.Id!.Value;
        var (artistName, name) = SplitTitle(result.Title!);

        return new StreamingSearchResult(
            StreamingResultType.Release,
            name,
            artistName,
            PickImage(result.CoverImage),
            [new ProviderLinkCandidate(PlatformCodes.Discogs, $"{entityType}:{id}", new Uri($"https://www.discogs.com/{entityType}/{id}"))],
            isMaster ? ReleaseType.Album : ToReleaseType(result.Format));
    }

    internal static (string ArtistName, string Name) SplitTitle(string title)
    {
        var separatorIndex = title.IndexOf(TitleSeparator, StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return (UnknownArtist, title);
        }

        var artistName = ArtistMarkerPattern().Replace(title[..separatorIndex], string.Empty).Trim();
        var name = title[(separatorIndex + TitleSeparator.Length)..];
        return (artistName.Length == 0 ? UnknownArtist : artistName, name);
    }

    // Community-entered data, like the title (see SplitTitle) — a malformed cover_image URL is
    // dropped rather than allowed to throw and discard the whole result.
    internal static Uri? PickImage(string? coverImage) =>
        !string.IsNullOrWhiteSpace(coverImage)
        && Uri.TryCreate(coverImage, UriKind.Absolute, out var uri)
        && !uri.AbsolutePath.EndsWith(PlaceholderImageFileName, StringComparison.OrdinalIgnoreCase)
            ? uri
            : null;

    internal static ReleaseType ToReleaseType(IReadOnlyList<string>? format)
    {
        var descriptors = format ?? [];
        return Has(descriptors, "Compilation") ? ReleaseType.Compilation
            : Has(descriptors, "Single") || Has(descriptors, "Maxi-Single") ? ReleaseType.Single
            : Has(descriptors, "EP") || Has(descriptors, "Mini-Album") ? ReleaseType.EP
            : Has(descriptors, "Live") ? ReleaseType.Live
            : ReleaseType.Album;
    }

    private static bool Has(IReadOnlyList<string> descriptors, string keyword) =>
        descriptors.Any(descriptor => string.Equals(descriptor, keyword, StringComparison.OrdinalIgnoreCase));

    // Discogs's numeric disambiguation suffix ("Placebo (3)") and name-variation asterisk ("Beyoncé*"),
    // after each credited artist — at the end of the name or before a join like " & " or ", ".
    [GeneratedRegex(@"\s\(\d+\)(?=\s|,|$)|\*(?=\s|,|$)")]
    private static partial Regex ArtistMarkerPattern();
}
