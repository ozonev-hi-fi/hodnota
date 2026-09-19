using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Hodnota.Application.Catalog;

// The matching key used by SearchResultMerger to recognize the same song/album across providers.
// See ADR 0011: this is a normalized-key comparison, not exact string equality and not a
// fuzzy-similarity score — every ambiguous call here is resolved toward NOT merging (a missed merge
// costs one extra row; a false merge sends someone to the wrong recording).
internal static partial class SearchResultKey
{
    internal readonly record struct MatchKey(StreamingResultType Type, string Artist, string Title);

    internal static MatchKey Build(StreamingSearchResult result)
    {
        var artistKey = NormalizeArtist(result.ArtistName);
        var titleKey = StripArtistFromTitle(Normalize(result.Name), artistKey);
        if (titleKey.Length == 0)
        {
            // Normalization stripped the title down to nothing (e.g. a title that's only noise text
            // like "(Remastered)"). An empty title is not a matching signal, so falling back to the
            // raw name keeps two different songs that both lost their whole title this way from
            // colliding into the same key.
            titleKey = result.Name?.Trim() ?? string.Empty;
        }

        return new MatchKey(result.Type, artistKey, titleKey);
    }

    internal static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.ToLowerInvariant();
        text = FoldDiacritics(text);
        text = FoldTypography(text);
        text = StripNoiseBrackets(text);
        text = StripTrailingNoiseSegments(text);
        text = StripFeaturedArtistSegments(text);
        text = StripPunctuation(text);
        return CollapseWhitespace(text);
    }

    internal static string NormalizeArtist(string? artistName)
    {
        if (string.IsNullOrWhiteSpace(artistName))
        {
            return string.Empty;
        }

        // Take only the first credited artist: display wants "Jay-Z, Alicia Keys", but matching
        // needs just "jayz", since YouTube's channel is "JayZVEVO" — one artist, never a list.
        // Deliberately not split on "&" — "Simon & Garfunkel" must survive as one artist.
        var firstSegment = ArtistSplitPattern().Split(artistName.Trim())[0];
        var normalized = Normalize(firstSegment);
        return ChannelSuffixPattern().Replace(normalized, string.Empty).TrimEnd();
    }

    private static string StripArtistFromTitle(string title, string artistKey) => true switch
    {
        _ when artistKey.Length == 0 || title.Length == 0 => title,
        _ when title.StartsWith(artistKey + " ", StringComparison.Ordinal) => title[(artistKey.Length + 1)..].Trim(),
        _ when title.EndsWith(" " + artistKey, StringComparison.Ordinal) => title[..^(artistKey.Length + 1)].Trim(),
        _ => title,
    };

    private static string FoldDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string FoldTypography(string text) => TypographyPattern().Replace(text, match => match.Value switch
    {
        "’" or "‘" => "'",
        "“" or "”" => "\"",
        "–" or "—" or "‒" or "−" => "-",
        _ => match.Value,
    });

    // Removes (...)/[...]/{...} groups whose entire inner text is noise — never a substring match —
    // repeating until stable so "(Official Music Video) [4K]" loses both groups.
    private static string StripNoiseBrackets(string text)
    {
        string previous;
        do
        {
            previous = text;
            text = BracketGroupPattern().Replace(text, match =>
            {
                var inner = (match.Groups[1].Success ? match.Groups[1].Value
                    : match.Groups[2].Success ? match.Groups[2].Value
                    : match.Groups[3].Value).Trim();
                return NoisePattern().IsMatch(inner) ? " " : match.Value;
            });
        } while (text != previous);

        return text;
    }

    // Drops a trailing " - <noise>" segment, e.g. Spotify's own "Nothing Else Matters - Remastered".
    private static string StripTrailingNoiseSegments(string text)
    {
        while (true)
        {
            var separatorIndex = text.LastIndexOf(" - ", StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                return text;
            }

            var lastSegment = text[(separatorIndex + 3)..].Trim();
            if (!NoisePattern().IsMatch(lastSegment))
            {
                return text;
            }

            text = text[..separatorIndex].TrimEnd();
        }
    }

    private static string StripFeaturedArtistSegments(string text)
    {
        text = FeaturedArtistBracketPattern().Replace(text, string.Empty);
        return FeaturedArtistTrailingPattern().Replace(text, string.Empty);
    }

    private static string StripPunctuation(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return builder.ToString();
    }

    private static string CollapseWhitespace(string text) => WhitespacePattern().Replace(text, " ").Trim();

    [GeneratedRegex(@"[‘’“”‒–—−]")]
    private static partial Regex TypographyPattern();

    [GeneratedRegex(@"\(([^()]*)\)|\[([^\[\]]*)\]|\{([^{}]*)\}")]
    private static partial Regex BracketGroupPattern();

    // Anchored full-match noise phrases. Deliberately excludes "live", "remix", "acoustic", "demo",
    // "instrumental", "karaoke", "cover", "radio edit", "extended", "unplugged", "session",
    // "re-recorded", "taylor's version" — those change what the recording IS, so stripping them
    // would cause a false merge between genuinely different recordings. See ADR 0011.
    [GeneratedRegex(
        @"^(?:(?:official\s+)?(?:hd\s+|hq\s+|4k\s+|uhd\s+)?(?:music\s+|lyrics?\s+)?(?:video|audio|visuali[sz]er|version)" +
        @"|(?:\d{4}\s+)?remaster(?:ed)?(?:\s+\d{4})?(?:\s+version)?" +
        @"|full\s+album(?:\s+stream)?" +
        @"|explicit|clean|lyrics|hd|hq|4k|uhd|mv|audio\s+only" +
        @"|(?:deluxe|expanded|anniversary|special)(?:\s+(?:edition|version))?" +
        @"|bonus\s+track(?:\s+version)?" +
        @"|mono(?:\s+version)?|stereo(?:\s+version)?)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex NoisePattern();

    [GeneratedRegex(@"\((?:feat|ft|featuring|with)\.?\s[^)]*\)", RegexOptions.IgnoreCase)]
    private static partial Regex FeaturedArtistBracketPattern();

    [GeneratedRegex(@"\s(?:feat|ft|featuring)\.?\s.*$", RegexOptions.IgnoreCase)]
    private static partial Regex FeaturedArtistTrailingPattern();

    [GeneratedRegex(@",|\bfeat\.?\b|\bft\.?\b|\bfeaturing\b|\sx\s", RegexOptions.IgnoreCase)]
    private static partial Regex ArtistSplitPattern();

    // Channel-name artifacts YouTube's ChannelTitle commonly carries: "<Artist> - Topic" (YouTube's
    // auto-generated artist channels), "<Artist>VEVO"/"<Artist> VEVO", "<Artist> Official".
    [GeneratedRegex(@"\s?(?:vevo|topic|official)$", RegexOptions.IgnoreCase)]
    private static partial Regex ChannelSuffixPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
