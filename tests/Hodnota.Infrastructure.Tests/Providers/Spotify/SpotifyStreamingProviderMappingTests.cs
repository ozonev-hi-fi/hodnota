using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;
using Hodnota.Infrastructure.Providers.Spotify;

namespace Hodnota.Infrastructure.Tests.Providers.Spotify;

// SpotifyStreamingProvider.SearchAsync itself needs the real HTTP client stack (see
// SpotifyApiClientTests/SpotifyAccessTokenProviderTests for that, via a hand-rolled test
// HttpMessageHandler), but the DTO -> StreamingSearchResult mapping (internal, see
// InternalsVisibleTo) is a plain pure function and is tested directly here.
public class SpotifyStreamingProviderMappingTests
{
    private static SpotifyTrack NewTrack(string id = "track-1") => new(
        id,
        "Nothing Else Matters",
        [new SpotifyArtist("Metallica")],
        new SpotifyAlbum(
            "album-1",
            "Metallica",
            "album",
            [new SpotifyArtist("Metallica")],
            [new SpotifyImage("https://example.com/640.jpg", 640, 640), new SpotifyImage("https://example.com/300.jpg", 300, 300), new SpotifyImage("https://example.com/64.jpg", 64, 64)],
            new SpotifyExternalUrls("https://open.spotify.com/album/album-1")),
        new SpotifyExternalUrls("https://open.spotify.com/track/track-1"));

    private static SpotifyAlbum NewAlbum(string id = "album-1", string? albumType = "album") => new(
        id,
        "Metallica",
        albumType,
        [new SpotifyArtist("Metallica")],
        [new SpotifyImage("https://example.com/300.jpg", 300, 300)],
        new SpotifyExternalUrls("https://open.spotify.com/album/album-1"));

    [Fact]
    public void ToSearchResult_TrackItem_MapsToTrackWithSpotifyLink()
    {
        var result = SpotifyStreamingProvider.ToSearchResult(NewTrack("track-1"));

        result.Type.Should().Be(StreamingResultType.Track);
        result.Name.Should().Be("Nothing Else Matters");
        result.ArtistName.Should().Be("Metallica");
        result.Links.Should().BeEquivalentTo(
        [
            new ProviderLinkCandidate(PlatformCodes.Spotify, "track-1", new Uri("https://open.spotify.com/track/track-1")),
        ]);
    }

    [Fact]
    public void ToSearchResult_TrackWithMultipleArtists_JoinsArtistNamesWithComma()
    {
        var track = NewTrack() with { Artists = [new SpotifyArtist("Jay-Z"), new SpotifyArtist("Alicia Keys")] };

        var result = SpotifyStreamingProvider.ToSearchResult(track);

        result.ArtistName.Should().Be("Jay-Z, Alicia Keys");
    }

    [Fact]
    public void ToSearchResult_AlbumItem_MapsToReleaseWithSpotifyLink()
    {
        var result = SpotifyStreamingProvider.ToSearchResult(NewAlbum("album-1"));

        result.Type.Should().Be(StreamingResultType.Release);
        result.Name.Should().Be("Metallica");
        result.ArtistName.Should().Be("Metallica");
        result.Links.Should().BeEquivalentTo(
        [
            new ProviderLinkCandidate(PlatformCodes.Spotify, "album-1", new Uri("https://open.spotify.com/album/album-1")),
        ]);
    }

    [Fact]
    public void ToSearchResult_AlbumTypeSingle_MapsToSingleReleaseType()
    {
        var result = SpotifyStreamingProvider.ToSearchResult(NewAlbum(albumType: "single"));

        result.ReleaseType.Should().Be(ReleaseType.Single);
    }

    [Fact]
    public void ToSearchResult_AlbumTypeCompilation_MapsToCompilationReleaseType()
    {
        var result = SpotifyStreamingProvider.ToSearchResult(NewAlbum(albumType: "compilation"));

        result.ReleaseType.Should().Be(ReleaseType.Compilation);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-future-type")]
    public void ToSearchResult_UnknownAlbumType_FallsBackToAlbumReleaseType(string? albumType)
    {
        var result = SpotifyStreamingProvider.ToSearchResult(NewAlbum(albumType: albumType));

        result.ReleaseType.Should().Be(ReleaseType.Album);
    }

    [Fact]
    public void ToSearchResult_MultipleImages_PicksNarrowestAtLeast300Wide()
    {
        var result = SpotifyStreamingProvider.ToSearchResult(NewTrack());

        result.ImageUrl.Should().Be(new Uri("https://example.com/300.jpg"));
    }

    [Fact]
    public void ToSearchResult_OnlySmallImages_PicksTheWidestAvailable()
    {
        var track = NewTrack() with
        {
            Album = NewTrack().Album! with
            {
                Images = [new SpotifyImage("https://example.com/64.jpg", 64, 64), new SpotifyImage("https://example.com/32.jpg", 32, 32)],
            },
        };

        var result = SpotifyStreamingProvider.ToSearchResult(track);

        result.ImageUrl.Should().Be(new Uri("https://example.com/64.jpg"));
    }

    [Fact]
    public void ToSearchResult_NoImages_ImageUrlIsNull()
    {
        var track = NewTrack() with { Album = NewTrack().Album! with { Images = [] } };

        var result = SpotifyStreamingProvider.ToSearchResult(track);

        result.ImageUrl.Should().BeNull();
    }

    [Fact]
    public void ToSearchResult_MissingExternalUrl_FallsBackToUrlBuiltFromId()
    {
        var track = NewTrack("track-2") with { ExternalUrls = null };

        var result = SpotifyStreamingProvider.ToSearchResult(track);

        result.Links[0].ExternalUrl.Should().Be(new Uri("https://open.spotify.com/track/track-2"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsUsable_BlankName_ReturnsFalse(string? name)
    {
        SpotifyStreamingProvider.IsUsable(NewTrack() with { Name = name }).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsUsable_MissingId_ReturnsFalse(string? id)
    {
        SpotifyStreamingProvider.IsUsable(NewTrack() with { Id = id }).Should().BeFalse();
    }

    [Fact]
    public void IsUsable_UsableTrack_ReturnsTrue()
    {
        SpotifyStreamingProvider.IsUsable(NewTrack()).Should().BeTrue();
    }

    [Fact]
    public void ProviderCode_IsSpotify()
    {
        new SpotifyStreamingProvider(null!).ProviderCode.Should().Be(ProviderCodes.Spotify);
    }
}
