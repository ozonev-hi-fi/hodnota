using AwesomeAssertions;

using Hodnota.Application.Catalog;

namespace Hodnota.Application.Tests.Catalog;

public class SearchResultMergerTests
{
    private static StreamingSearchResult NewResult(
        string name,
        string artist,
        StreamingResultType type = StreamingResultType.Track,
        params (string PlatformCode, string ExternalId)[] links) =>
        new(
            type,
            name,
            artist,
            null,
            [.. links.Select(l => new ProviderLinkCandidate(l.PlatformCode, l.ExternalId, new Uri($"https://example.com/{l.PlatformCode}/{l.ExternalId}")))]);

    [Fact]
    public void Merge_SameTrackFromTwoProviders_ReturnsOneRowCarryingBothProvidersLinks()
    {
        var spotify = NewResult("Nothing Else Matters", "Metallica", links: [("spotify", "sp1")]);
        var youTube = NewResult(
            "Metallica - Nothing Else Matters",
            "Metallica",
            links: [("youtube", "yt1"), ("youtube-music", "yt1")]);

        var merged = SearchResultMerger.Merge([[spotify], [youTube]]);

        merged.Should().ContainSingle();
        merged[0].Links.Select(l => l.PlatformCode).Should().Equal("spotify", "youtube", "youtube-music");
    }

    [Fact]
    public void Merge_MergedRow_KeepsTheHighestTrustProvidersNameArtistAndImage()
    {
        var spotify = NewResult("Nothing Else Matters", "Metallica", links: [("spotify", "sp1")]) with
        {
            ImageUrl = new Uri("https://example.com/spotify-image.jpg"),
        };
        var youTube = NewResult("Metallica - Nothing Else Matters", "MetallicaVEVO", links: [("youtube", "yt1")]) with
        {
            ImageUrl = new Uri("https://example.com/youtube-image.jpg"),
        };

        var merged = SearchResultMerger.Merge([[spotify], [youTube]]);

        merged[0].Name.Should().Be("Nothing Else Matters");
        merged[0].ArtistName.Should().Be("Metallica");
        merged[0].ImageUrl.Should().Be(new Uri("https://example.com/spotify-image.jpg"));
    }

    [Fact]
    public void Merge_DuplicateLinksForTheSamePlatform_KeepsOnlyTheFirst()
    {
        var spine = NewResult("Nothing Else Matters", "Metallica", links: [("youtube", "id1")]);
        var duplicate = NewResult("Metallica - Nothing Else Matters", "Metallica", links: [("youtube", "id2")]);

        var merged = SearchResultMerger.Merge([[spine], [duplicate]]);

        merged.Should().ContainSingle();
        merged[0].Links.Should().ContainSingle();
        merged[0].Links[0].ExternalId.Should().Be("id1");
    }

    [Fact]
    public void Merge_DuplicateResultsWithinOneProvider_AreCollapsedIntoOneRow()
    {
        var original = NewResult("Nothing Else Matters", "Metallica", links: [("spotify", "id1")]);
        var remaster = NewResult("Nothing Else Matters - Remastered", "Metallica", links: [("spotify", "id2")]);

        var merged = SearchResultMerger.Merge([[original, remaster]]);

        merged.Should().ContainSingle();
        merged[0].Name.Should().Be("Nothing Else Matters");
        merged[0].Links.Should().ContainSingle();
        merged[0].Links[0].ExternalId.Should().Be("id1");
    }

    [Fact]
    public void Merge_UnmatchedLowerTrustResults_AreAppendedAfterTheSpineRows()
    {
        var spineResult = NewResult("Nothing Else Matters", "Metallica", links: [("spotify", "sp1")]);
        var unmatched = NewResult("Some Live Bootleg", "Metallica", links: [("youtube", "yt1")]);

        var merged = SearchResultMerger.Merge([[spineResult], [unmatched]]);

        merged.Should().HaveCount(2);
        merged[0].Name.Should().Be("Nothing Else Matters");
        merged[1].Name.Should().Be("Some Live Bootleg");
    }

    [Fact]
    public void Merge_SpineProviderResultOrder_IsPreserved()
    {
        var first = NewResult("Master of Puppets", "Metallica", links: [("spotify", "sp1")]);
        var second = NewResult("One", "Metallica", links: [("spotify", "sp2")]);

        var merged = SearchResultMerger.Merge([[first, second], []]);

        merged.Select(r => r.Name).Should().Equal("Master of Puppets", "One");
    }

    [Fact]
    public void Merge_TrackAndReleaseWithIdenticalNameAndArtist_AreNotMerged()
    {
        var track = NewResult("Metallica", "Metallica", StreamingResultType.Track, ("spotify", "track-1"));
        var release = NewResult("Metallica", "Metallica", StreamingResultType.Release, ("spotify", "album-1"));

        var merged = SearchResultMerger.Merge([[track], [release]]);

        merged.Should().HaveCount(2);
    }

    [Fact]
    public void Merge_TwoLowerTrustResultsMatchingTheSameSpineRow_ProduceOneRow()
    {
        var spine = NewResult("Nothing Else Matters", "Metallica", links: [("spotify", "sp1")]);
        var youTubeVariant1 = NewResult("Metallica - Nothing Else Matters", "Metallica", links: [("youtube", "yt1")]);
        var youTubeVariant2 = NewResult("Nothing Else Matters", "Metallica - Topic", links: [("youtube-music", "yt1")]);

        var merged = SearchResultMerger.Merge([[spine], [youTubeVariant1, youTubeVariant2]]);

        merged.Should().ContainSingle();
        merged[0].Links.Select(l => l.PlatformCode).Should().Equal("spotify", "youtube", "youtube-music");
    }

    [Fact]
    public void Merge_HighestTrustProviderReturnsNothing_NextProviderProvidesTheSpine()
    {
        var youTube = NewResult("Nothing Else Matters", "Metallica", links: [("youtube", "yt1")]);

        var merged = SearchResultMerger.Merge([[], [youTube]]);

        merged.Should().ContainSingle();
        merged[0].Name.Should().Be("Nothing Else Matters");
        merged[0].Links.Should().ContainSingle(l => l.PlatformCode == "youtube");
    }

    [Fact]
    public void Merge_NoProviderReturnsAnything_ReturnsEmpty()
    {
        var merged = SearchResultMerger.Merge([[], []]);

        merged.Should().BeEmpty();
    }
}
