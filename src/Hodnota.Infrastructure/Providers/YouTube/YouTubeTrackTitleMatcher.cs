using System.Text.RegularExpressions;

using Hodnota.Application.Catalog;

namespace Hodnota.Infrastructure.Providers.YouTube;

internal static partial class YouTubeTrackTitleMatcher
{
    internal static string Key(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        string previous;
        do
        {
            previous = title;
            title = TrailingVariantPattern().Replace(title, string.Empty);
        } while (title != previous);

        return SearchResultKey.Normalize(title);
    }

    internal static bool IsAnyKnownTrack(string? title, IReadOnlyList<string> knownTitles)
    {
        var key = Key(title);
        return key.Length > 0 && knownTitles.Any(known => Key(known) == key);
    }

    [GeneratedRegex(@"\s*(?:\([^()]*\)|\[[^\[\]]*\]|\s[-–—]\s.*)\s*$")]
    private static partial Regex TrailingVariantPattern();
}
