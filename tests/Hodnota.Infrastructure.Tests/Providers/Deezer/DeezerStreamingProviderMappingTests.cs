using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;
using Hodnota.Infrastructure.Providers.Deezer;

namespace Hodnota.Infrastructure.Tests.Providers.Deezer;

// DeezerStreamingProvider.SearchAsync/LookupAsync need the real HTTP client stack (see
// DeezerApiClientTests and DeezerStreamingProvider{Search,Lookup}Tests), but the DTO ->
// StreamingSearchResult mapping (internal, see InternalsVisibleTo) is a plain pure function and is
// tested directly here.
public class DeezerStreamingProviderMappingTests
{
    private static DeezerTrack NewTrack(long? id = 1) => new(
        id,
        "Enter Sandman (Remastered 2021)",
        "QMKHM1900001",
        "https://www.deezer.com/track/1",
        new DeezerArtist("Metallica"),
        new DeezerAlbum(10, "Metallica (Remastered 2021)", null, null, "album", "https://example.com/medium.jpg", "https://example.com/big.jpg", "https://example.com/xl.jpg", null));

    private static DeezerAlbum NewAlbum(long? id = 1, string? recordType = "album") => new(
        id,
        "Load (Remastered)",
        "602475158875",
        "https://www.deezer.com/album/1",
        recordType,
        "https://example.com/medium.jpg",
        "https://example.com/big.jpg",
        "https://example.com/xl.jpg",
        new DeezerArtist("Metallica"));

    [Fact]
    public void ToSearchResult_TrackItem_MapsToTrackWithDeezerLink()
    {
        var result = DeezerStreamingProvider.ToSearchResult(NewTrack(1));

        result.Type.Should().Be(StreamingResultType.Track);
        result.Name.Should().Be("Enter Sandman (Remastered 2021)");
        result.ArtistName.Should().Be("Metallica");
        result.Isrc.Should().Be("QMKHM1900001");
        result.Links.Should().BeEquivalentTo(
        [
            new ProviderLinkCandidate(PlatformCodes.Deezer, "1", new Uri("https://www.deezer.com/track/1")),
        ]);
    }

    [Fact]
    public void ToSearchResult_TrackMissingArtist_ArtistNameIsUnknown()
    {
        var track = NewTrack() with { Artist = null };

        var result = DeezerStreamingProvider.ToSearchResult(track);

        result.ArtistName.Should().Be("Unknown");
    }

    [Fact]
    public void ToSearchResult_TrackMissingLink_FallsBackToBuiltUrl()
    {
        var track = NewTrack() with { Link = null };

        var result = DeezerStreamingProvider.ToSearchResult(track);

        result.Links.Single().ExternalUrl.Should().Be(new Uri("https://www.deezer.com/track/1"));
    }

    [Fact]
    public void ToSearchResult_AlbumItem_MapsToReleaseWithDeezerLink()
    {
        var result = DeezerStreamingProvider.ToSearchResult(NewAlbum(1));

        result.Type.Should().Be(StreamingResultType.Release);
        result.Name.Should().Be("Load (Remastered)");
        result.ArtistName.Should().Be("Metallica");
        result.Upc.Should().Be("602475158875");
        result.Links.Should().BeEquivalentTo(
        [
            new ProviderLinkCandidate(PlatformCodes.Deezer, "1", new Uri("https://www.deezer.com/album/1")),
        ]);
    }

    [Fact]
    public void ToSearchResult_AlbumMissingArtist_ArtistNameIsUnknown()
    {
        var album = NewAlbum() with { Artist = null };

        var result = DeezerStreamingProvider.ToSearchResult(album);

        result.ArtistName.Should().Be("Unknown");
    }

    [Theory]
    [InlineData("single", ReleaseType.Single)]
    [InlineData("ep", ReleaseType.EP)]
    [InlineData("compile", ReleaseType.Compilation)]
    [InlineData("album", ReleaseType.Album)]
    public void ToSearchResult_RecordType_MapsToExpectedReleaseType(string recordType, ReleaseType expected)
    {
        var result = DeezerStreamingProvider.ToSearchResult(NewAlbum(recordType: recordType));

        result.ReleaseType.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-future-type")]
    public void ToSearchResult_UnknownRecordType_FallsBackToAlbumReleaseType(string? recordType)
    {
        var result = DeezerStreamingProvider.ToSearchResult(NewAlbum(recordType: recordType));

        result.ReleaseType.Should().Be(ReleaseType.Album);
    }

    [Fact]
    public void PickImage_CoverBigPresent_PicksCoverBig()
    {
        DeezerStreamingProvider.PickImage(NewAlbum()).Should().Be(new Uri("https://example.com/big.jpg"));
    }

    [Fact]
    public void PickImage_CoverBigMissing_FallsBackToCoverXl()
    {
        var album = NewAlbum() with { CoverBig = null };

        DeezerStreamingProvider.PickImage(album).Should().Be(new Uri("https://example.com/xl.jpg"));
    }

    [Fact]
    public void PickImage_OnlyCoverMediumAvailable_FallsBackToCoverMedium()
    {
        var album = NewAlbum() with { CoverBig = null, CoverXl = null };

        DeezerStreamingProvider.PickImage(album).Should().Be(new Uri("https://example.com/medium.jpg"));
    }

    [Fact]
    public void PickImage_NoAlbum_ReturnsNull()
    {
        DeezerStreamingProvider.PickImage(null).Should().BeNull();
    }

    [Fact]
    public void PickImage_NoCovers_ReturnsNull()
    {
        var album = NewAlbum() with { CoverBig = null, CoverXl = null, CoverMedium = null };

        DeezerStreamingProvider.PickImage(album).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsUsable_BlankTrackTitle_ReturnsFalse(string? title)
    {
        DeezerStreamingProvider.IsUsable(NewTrack() with { Title = title }).Should().BeFalse();
    }

    [Fact]
    public void IsUsable_MissingTrackId_ReturnsFalse()
    {
        DeezerStreamingProvider.IsUsable(NewTrack() with { Id = null }).Should().BeFalse();
    }

    [Fact]
    public void IsUsable_UsableTrack_ReturnsTrue()
    {
        DeezerStreamingProvider.IsUsable(NewTrack()).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsUsable_BlankAlbumTitle_ReturnsFalse(string? title)
    {
        DeezerStreamingProvider.IsUsable(NewAlbum() with { Title = title }).Should().BeFalse();
    }

    [Fact]
    public void IsUsable_MissingAlbumId_ReturnsFalse()
    {
        DeezerStreamingProvider.IsUsable(NewAlbum() with { Id = null }).Should().BeFalse();
    }

    [Fact]
    public void IsUsable_UsableAlbum_ReturnsTrue()
    {
        DeezerStreamingProvider.IsUsable(NewAlbum()).Should().BeTrue();
    }

    [Fact]
    public void ProviderCode_IsDeezer()
    {
        new DeezerStreamingProvider(null!).ProviderCode.Should().Be(ProviderCodes.Deezer);
    }

    [Fact]
    public void SupportsAndSupportsLookup_BothTypes()
    {
        var provider = new DeezerStreamingProvider(null!);

        provider.Supports(StreamingResultType.Track).Should().BeTrue();
        provider.Supports(StreamingResultType.Release).Should().BeTrue();
        provider.SupportsLookup(StreamingResultType.Track).Should().BeTrue();
        provider.SupportsLookup(StreamingResultType.Release).Should().BeTrue();
        provider.LinkPlatformCodes.Should().Equal(PlatformCodes.Deezer);
    }
}
