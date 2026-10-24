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

        while (true)
        {
            var match = TrailingVariantPattern().Match(title);
            if (!match.Success)
            {
                break;
            }

            var inner = (match.Groups[1].Success ? match.Groups[1].Value
                : match.Groups[2].Success ? match.Groups[2].Value
                : match.Groups[3].Value).Trim();
            // Stop at (don't strip) a trailing segment that names a genuinely different recording —
            // same ADR 0011 rationale as SearchResultKey.Normalize's noise list — so a live/remix
            // upload never collapses to the same key as the studio track.
            if (RecordingVariantPattern().IsMatch(inner))
            {
                break;
            }

            title = title[..match.Index].TrimEnd();
        }

        return SearchResultKey.Normalize(title);
    }

    internal static bool IsAnyKnownTrack(string? title, IReadOnlyList<string> knownTitles)
    {
        var key = Key(title);
        return key.Length > 0 && knownTitles.Any(known => Key(known) == key);
    }

    [GeneratedRegex(@"\s*(?:\(([^()]*)\)|\[([^\[\]]*)\]|\s[-–—]\s(.*))\s*$")]
    private static partial Regex TrailingVariantPattern();

    [GeneratedRegex(
        @"\b(?:live|remix|acoustic|demo|instrumental|karaoke|cover|radio edit|extended|unplugged|session|re-recorded|taylor's version)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex RecordingVariantPattern();
}
