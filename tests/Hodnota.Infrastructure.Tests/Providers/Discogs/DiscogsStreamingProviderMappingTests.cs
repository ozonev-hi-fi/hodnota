using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;
using Hodnota.Infrastructure.Providers.Discogs;

namespace Hodnota.Infrastructure.Tests.Providers.Discogs;

// DiscogsStreamingProvider.SearchAsync itself needs the real HTTP client stack (see
// DiscogsStreamingProviderSearchTests, via a hand-rolled test HttpMessageHandler), but the DTO ->
// StreamingSearchResult mapping (internal, see InternalsVisibleTo) is a plain pure function and is
// tested directly here.
public class DiscogsStreamingProviderMappingTests
{
    private static DiscogsSearchResult NewResult(
        long? id = 1,
        string? type = "release",
        string? title = "Metallica - Metallica",
        IReadOnlyList<string>? format = null,
        long? masterId = null,
        string? coverImage = "https://example.com/cover.jpg") =>
        new(id, type, title, format ?? ["Vinyl", "LP", "Album"], masterId, coverImage);

    [Fact]
    public void ToSearchResult_ReleaseItem_MapsToReleaseWithPrefixedIdAndReleaseLink()
    {
        var result = DiscogsStreamingProvider.ToSearchResult(NewResult(id: 123, type: "release"));

        result.Type.Should().Be(StreamingResultType.Release);
        result.ArtistName.Should().Be("Metallica");
        result.Name.Should().Be("Metallica");
        result.Links.Should().BeEquivalentTo(
        [
            new ProviderLinkCandidate(PlatformCodes.Discogs, "release:123", new Uri("https://www.discogs.com/release/123")),
        ]);
    }

    [Fact]
    public void ToSearchResult_MasterItem_MapsToReleaseWithPrefixedIdAndMasterLink()
    {
        var result = DiscogsStreamingProvider.ToSearchResult(NewResult(id: 456, type: "master", masterId: 456));

        result.Type.Should().Be(StreamingResultType.Release);
        result.Links.Should().BeEquivalentTo(
        [
            new ProviderLinkCandidate(PlatformCodes.Discogs, "master:456", new Uri("https://www.discogs.com/master/456")),
        ]);
    }

    [Fact]
    public void ToSearchResult_MasterAndReleaseWithSameNumber_GetDifferentExternalIds()
    {
        var master = DiscogsStreamingProvider.ToSearchResult(NewResult(id: 13814, type: "master"));
        var release = DiscogsStreamingProvider.ToSearchResult(NewResult(id: 13814, type: "release"));

        master.Links[0].ExternalId.Should().NotBe(release.Links[0].ExternalId);
    }

    [Fact]
    public void ToSearchResult_TitleWithoutSeparator_ArtistIsUnknownAndNameIsWholeTitle()
    {
        var result = DiscogsStreamingProvider.ToSearchResult(NewResult(title: "Some Compilation Title"));

        result.ArtistName.Should().Be("Unknown");
        result.Name.Should().Be("Some Compilation Title");
    }

    [Fact]
    public void ToSearchResult_TitleStartingWithSeparator_ArtistIsUnknownRatherThanEmpty()
    {
        var result = DiscogsStreamingProvider.ToSearchResult(NewResult(title: " - Nothing Else Matters"));

        result.ArtistName.Should().Be("Unknown");
        result.Name.Should().Be("Nothing Else Matters");
    }

    [Fact]
    public void ToSearchResult_TitleWithMultipleSeparators_SplitsOnFirstOnly()
    {
        var result = DiscogsStreamingProvider.ToSearchResult(NewResult(title: "Artist - Title - Deluxe Edition"));

        result.ArtistName.Should().Be("Artist");
        result.Name.Should().Be("Title - Deluxe Edition");
    }

    [Theory]
    [InlineData("Placebo (3) - Meds", "Placebo")]
    [InlineData("Beyoncé* - 4", "Beyoncé")]
    [InlineData("Jay-Z* & Kanye West (2) - Watch The Throne", "Jay-Z & Kanye West")]
    [InlineData("Artist (12), Other* - Split", "Artist, Other")]
    [InlineData("Blink-182 - Enema Of The State", "Blink-182")]
    [InlineData("*NSYNC - No Strings Attached", "*NSYNC")]
    [InlineData("Sunn O))) - Monoliths & Dimensions", "Sunn O)))")]
    [InlineData("Boney M. (Remixed) - Hits", "Boney M. (Remixed)")]
    public void ToSearchResult_ArtistWithDiscogsMarkers_StripsOnlyTheMarkers(string title, string expectedArtist)
    {
        DiscogsStreamingProvider.ToSearchResult(NewResult(title: title)).ArtistName.Should().Be(expectedArtist);
    }

    [Fact]
    public void ToSearchResult_ArtistThatIsOnlyAMarker_IsUnknown()
    {
        DiscogsStreamingProvider.ToSearchResult(NewResult(title: "* - Title")).ArtistName.Should().Be("Unknown");
    }

    [Fact]
    public void ToSearchResult_UpcAndIsrc_AreAlwaysNull()
    {
        var result = DiscogsStreamingProvider.ToSearchResult(NewResult());

        result.Upc.Should().BeNull();
        result.Isrc.Should().BeNull();
    }

    [Theory]
    [InlineData("Compilation", ReleaseType.Compilation)]
    [InlineData("Single", ReleaseType.Single)]
    [InlineData("EP", ReleaseType.EP)]
    [InlineData("Live", ReleaseType.Live)]
    [InlineData("compilation", ReleaseType.Compilation)]
    public void ToSearchResult_ReleaseFormatKeyword_MapsToMatchingReleaseType(string keyword, ReleaseType expected)
    {
        var result = DiscogsStreamingProvider.ToSearchResult(NewResult(type: "release", format: ["Vinyl", keyword]));

        result.ReleaseType.Should().Be(expected);
    }

    [Fact]
    public void ToSearchResult_MasterFormatKeyword_IsIgnoredAndReleaseTypeIsAlbum()
    {
        var result = DiscogsStreamingProvider.ToSearchResult(NewResult(type: "master", format: ["CD", "Compilation"]));

        result.ReleaseType.Should().Be(ReleaseType.Album);
    }

    [Fact]
    public void ToSearchResult_NullFormat_FallsBackToAlbumReleaseType()
    {
        var result = NewResult() with { Format = null };

        DiscogsStreamingProvider.ToSearchResult(result).ReleaseType.Should().Be(ReleaseType.Album);
    }

    [Fact]
    public void ToSearchResult_EmptyFormat_FallsBackToAlbumReleaseType()
    {
        var result = NewResult() with { Format = [] };

        DiscogsStreamingProvider.ToSearchResult(result).ReleaseType.Should().Be(ReleaseType.Album);
    }

    [Fact]
    public void ToSearchResult_UnrecognizedFormatKeywords_FallsBackToAlbumReleaseType()
    {
        DiscogsStreamingProvider.ToSearchResult(NewResult(format: ["Vinyl", "LP"])).ReleaseType.Should().Be(ReleaseType.Album);
    }

    [Fact]
    public void ToSearchResult_NoCoverImage_ImageUrlIsNull()
    {
        var result = DiscogsStreamingProvider.ToSearchResult(NewResult(coverImage: null));

        result.ImageUrl.Should().BeNull();
    }

    [Fact]
    public void ToSearchResult_WithCoverImage_MapsImageUrl()
    {
        var result = DiscogsStreamingProvider.ToSearchResult(NewResult(coverImage: "https://example.com/cover.jpg"));

        result.ImageUrl.Should().Be(new Uri("https://example.com/cover.jpg"));
    }

    [Fact]
    public void ToSearchResult_PlaceholderCoverImage_ImageUrlIsNull()
    {
        var result = DiscogsStreamingProvider.ToSearchResult(NewResult(coverImage: "https://st.discogs.com/images/spacer.gif"));

        result.ImageUrl.Should().BeNull();
    }

    [Fact]
    public void ToSearchResult_MalformedCoverImage_ImageUrlIsNullRatherThanThrowing()
    {
        var result = DiscogsStreamingProvider.ToSearchResult(NewResult(coverImage: "not a valid url"));

        result.ImageUrl.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsUsable_BlankTitle_ReturnsFalse(string? title)
    {
        DiscogsStreamingProvider.IsUsable(NewResult(title: title)).Should().BeFalse();
    }

    [Fact]
    public void IsUsable_MissingId_ReturnsFalse()
    {
        DiscogsStreamingProvider.IsUsable(NewResult(id: null)).Should().BeFalse();
    }

    [Theory]
    [InlineData("artist")]
    [InlineData("label")]
    [InlineData(null)]
    public void IsUsable_NonReleaseType_ReturnsFalse(string? type)
    {
        DiscogsStreamingProvider.IsUsable(NewResult(type: type)).Should().BeFalse();
    }

    [Fact]
    public void IsUsable_Master_ReturnsTrue()
    {
        DiscogsStreamingProvider.IsUsable(NewResult(type: "master", masterId: 1)).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    public void IsUsable_ReleaseWithoutMaster_ReturnsTrue(long? masterId)
    {
        DiscogsStreamingProvider.IsUsable(NewResult(type: "release", masterId: masterId)).Should().BeTrue();
    }

    [Fact]
    public void IsUsable_ReleaseThatBelongsToAMaster_ReturnsFalse()
    {
        DiscogsStreamingProvider.IsUsable(NewResult(type: "release", masterId: 42)).Should().BeFalse();
    }

    [Fact]
    public void ProviderCode_IsDiscogs()
    {
        new DiscogsStreamingProvider(null!).ProviderCode.Should().Be(ProviderCodes.Discogs);
    }
}
