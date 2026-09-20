using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;
using Hodnota.Infrastructure.Providers.Qobuz;

namespace Hodnota.Infrastructure.Tests.Providers.Qobuz;

// QobuzStreamingProvider.SearchAsync itself needs the real HTTP client stack (see
// QobuzApiClientTests, via a hand-rolled test HttpMessageHandler), but the DTO ->
// StreamingSearchResult mapping (internal, see InternalsVisibleTo) is a plain pure function and is
// tested directly here.
public class QobuzStreamingProviderMappingTests
{
    private static QobuzTrack NewTrack(int? id = 1) => new(
        id,
        "Nothing Else Matters",
        "USRC17607839",
        new QobuzArtist("Metallica"),
        new QobuzAlbum(
            "album-1",
            "Metallica",
            "042284197928",
            new QobuzArtist("Metallica"),
            new QobuzImage("https://example.com/large.jpg", "https://example.com/medium.jpg", "https://example.com/thumb.jpg"),
            "album"));

    private static QobuzAlbum NewAlbum(string? id = "album-1", string? releaseType = "album") => new(
        id,
        "Metallica",
        "042284197928",
        new QobuzArtist("Metallica"),
        new QobuzImage("https://example.com/large.jpg", "https://example.com/medium.jpg", "https://example.com/thumb.jpg"),
        releaseType);

    [Fact]
    public void ToSearchResult_TrackItem_MapsToTrackWithQobuzLink()
    {
        var result = QobuzStreamingProvider.ToSearchResult(NewTrack(1));

        result.Type.Should().Be(StreamingResultType.Track);
        result.Name.Should().Be("Nothing Else Matters");
        result.ArtistName.Should().Be("Metallica");
        result.Isrc.Should().Be("USRC17607839");
        result.Links.Should().BeEquivalentTo(
        [
            new ProviderLinkCandidate(PlatformCodes.Qobuz, "1", new Uri("https://open.qobuz.com/track/1")),
        ]);
    }

    [Fact]
    public void ToSearchResult_TrackMissingPerformer_ArtistNameIsUnknown()
    {
        var track = NewTrack() with { Performer = null };

        var result = QobuzStreamingProvider.ToSearchResult(track);

        result.ArtistName.Should().Be("Unknown");
    }

    [Fact]
    public void ToSearchResult_AlbumItem_MapsToReleaseWithQobuzLink()
    {
        var result = QobuzStreamingProvider.ToSearchResult(NewAlbum("album-1"));

        result.Type.Should().Be(StreamingResultType.Release);
        result.Name.Should().Be("Metallica");
        result.ArtistName.Should().Be("Metallica");
        result.Upc.Should().Be("042284197928");
        result.Links.Should().BeEquivalentTo(
        [
            new ProviderLinkCandidate(PlatformCodes.Qobuz, "album-1", new Uri("https://open.qobuz.com/album/album-1")),
        ]);
    }

    [Fact]
    public void ToSearchResult_AlbumMissingArtist_ArtistNameIsUnknown()
    {
        var album = NewAlbum() with { Artist = null };

        var result = QobuzStreamingProvider.ToSearchResult(album);

        result.ArtistName.Should().Be("Unknown");
    }

    [Fact]
    public void ToSearchResult_AlbumReleaseTypeSingle_MapsToSingleReleaseType()
    {
        var result = QobuzStreamingProvider.ToSearchResult(NewAlbum(releaseType: "single"));

        result.ReleaseType.Should().Be(ReleaseType.Single);
    }

    [Fact]
    public void ToSearchResult_AlbumReleaseTypeCompilation_MapsToCompilationReleaseType()
    {
        var result = QobuzStreamingProvider.ToSearchResult(NewAlbum(releaseType: "compilation"));

        result.ReleaseType.Should().Be(ReleaseType.Compilation);
    }

    [Fact]
    public void ToSearchResult_AlbumReleaseTypeEp_MapsToEpReleaseType()
    {
        var result = QobuzStreamingProvider.ToSearchResult(NewAlbum(releaseType: "ep"));

        result.ReleaseType.Should().Be(ReleaseType.EP);
    }

    [Fact]
    public void ToSearchResult_AlbumReleaseTypeLive_MapsToLiveReleaseType()
    {
        var result = QobuzStreamingProvider.ToSearchResult(NewAlbum(releaseType: "live"));

        result.ReleaseType.Should().Be(ReleaseType.Live);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-future-type")]
    public void ToSearchResult_UnknownReleaseType_FallsBackToAlbumReleaseType(string? releaseType)
    {
        var result = QobuzStreamingProvider.ToSearchResult(NewAlbum(releaseType: releaseType));

        result.ReleaseType.Should().Be(ReleaseType.Album);
    }

    [Fact]
    public void ToSearchResult_ImageWithLarge_PicksLarge()
    {
        var result = QobuzStreamingProvider.ToSearchResult(NewAlbum());

        result.ImageUrl.Should().Be(new Uri("https://example.com/large.jpg"));
    }

    [Fact]
    public void ToSearchResult_ImageMissingLarge_FallsBackToMedium()
    {
        var album = NewAlbum() with { Image = NewAlbum().Image! with { Large = null } };

        var result = QobuzStreamingProvider.ToSearchResult(album);

        result.ImageUrl.Should().Be(new Uri("https://example.com/medium.jpg"));
    }

    [Fact]
    public void ToSearchResult_OnlyThumbnailAvailable_FallsBackToThumbnail()
    {
        var album = NewAlbum() with { Image = new QobuzImage(null, null, "https://example.com/thumb.jpg") };

        var result = QobuzStreamingProvider.ToSearchResult(album);

        result.ImageUrl.Should().Be(new Uri("https://example.com/thumb.jpg"));
    }

    [Fact]
    public void ToSearchResult_NoImage_ImageUrlIsNull()
    {
        var album = NewAlbum() with { Image = null };

        var result = QobuzStreamingProvider.ToSearchResult(album);

        result.ImageUrl.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsUsable_BlankTitle_ReturnsFalse(string? title)
    {
        QobuzStreamingProvider.IsUsable(NewTrack() with { Title = title }).Should().BeFalse();
    }

    [Fact]
    public void IsUsable_MissingTrackId_ReturnsFalse()
    {
        QobuzStreamingProvider.IsUsable(NewTrack() with { Id = null }).Should().BeFalse();
    }

    [Fact]
    public void IsUsable_UsableTrack_ReturnsTrue()
    {
        QobuzStreamingProvider.IsUsable(NewTrack()).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsUsable_MissingAlbumId_ReturnsFalse(string? id)
    {
        QobuzStreamingProvider.IsUsable(NewAlbum(id: id)).Should().BeFalse();
    }

    [Fact]
    public void IsUsable_UsableAlbum_ReturnsTrue()
    {
        QobuzStreamingProvider.IsUsable(NewAlbum()).Should().BeTrue();
    }

    [Fact]
    public void ProviderCode_IsQobuz()
    {
        new QobuzStreamingProvider(null!).ProviderCode.Should().Be(ProviderCodes.Qobuz);
    }
}
