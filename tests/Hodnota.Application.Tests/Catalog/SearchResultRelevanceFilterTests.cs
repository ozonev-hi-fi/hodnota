using AwesomeAssertions;

using Hodnota.Application.Catalog;

namespace Hodnota.Application.Tests.Catalog;

public class SearchResultRelevanceFilterTests
{
    private static StreamingSearchResult NewResult(string name, string artist) => new(StreamingResultType.Track, name, artist, null, []);

    [Fact]
    public void IsRelevant_ResultSharesAQueryWord_ReturnsTrue()
    {
        var result = NewResult("Aurora - A Different Kind Of Human (FULL album)", "Kamil Geisler");

        SearchResultRelevanceFilter.IsRelevant("aurora different kind", result).Should().BeTrue();
    }

    [Fact]
    public void IsRelevant_ResultSharesNoQueryWord_ReturnsFalse()
    {
        var result = NewResult("Демонтаж глобализма, или Почему всех устраивает хаос", "Аврора_17.0");

        SearchResultRelevanceFilter.IsRelevant("aurora different kind", result).Should().BeFalse();
    }

    [Fact]
    public void IsRelevant_MatchOnlyInArtistName_ReturnsTrue()
    {
        var result = NewResult("Unrelated Title", "Aurora Official");

        SearchResultRelevanceFilter.IsRelevant("aurora different kind", result).Should().BeTrue();
    }

    [Fact]
    public void IsRelevant_QueryHasOnlyShortWords_ReturnsTrueForAnyResult()
    {
        var result = NewResult("Completely Unrelated Title", "Some Channel");

        SearchResultRelevanceFilter.IsRelevant("u2", result).Should().BeTrue();
    }

    [Fact]
    public void IsRelevant_ResultNameAndArtistAreEmpty_ReturnsFalse()
    {
        var result = NewResult(string.Empty, string.Empty);

        SearchResultRelevanceFilter.IsRelevant("aurora different kind", result).Should().BeFalse();
    }
}
