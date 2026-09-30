using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;
using Hodnota.Infrastructure.Providers.Tidal;

using IncludedIndex = System.Collections.Generic.IReadOnlyDictionary<(string Type, string Id), Hodnota.Infrastructure.Providers.Tidal.TidalResource>;

namespace Hodnota.Infrastructure.Tests.Providers.Tidal;

// TidalStreamingProvider.SearchAsync itself needs the real HTTP client stack (see
// TidalStreamingProviderSearchTests and TidalApiClientTests, via a hand-rolled test
// HttpMessageHandler), but the JSON:API -> StreamingSearchResult mapping (internal, see
// InternalsVisibleTo) is a plain pure function and is tested directly here.
public class TidalStreamingProviderMappingTests
{
    private static TidalAttributes Attributes(
        string? title = null,
        string? version = null,
        string? isrc = null,
        string? barcodeId = null,
        string? albumType = null,
        string? name = null,
        IReadOnlyList<TidalExternalLink>? externalLinks = null,
        IReadOnlyList<TidalFile>? files = null) =>
        new(title, version, isrc, barcodeId, albumType, name, externalLinks, files);

    private static TidalRelationship Reference(string type, params string[] ids) =>
        new([.. ids.Select(id => new TidalResourceReference(id, type))]);

    private static TidalResource Resource(string type, string id, TidalAttributes attributes, params (string Name, TidalRelationship Relationship)[] relationships) =>
        new(id, type, attributes, relationships.ToDictionary(r => r.Name, r => r.Relationship));

    private static IncludedIndex Index(params TidalResource[] included) => TidalStreamingProvider.IndexIncluded(included);

    private static TidalExternalLink SharingLink(string href) => new(href, new TidalLinkMeta("TIDAL_SHARING"));

    private static TidalFile File(int width) => new($"https://resources.example.com/{width}x{width}.jpg", new TidalFileMeta(width, width));

    private static (TidalResource Track, IncludedIndex Included) NewTrack(string? version = null, IReadOnlyList<TidalExternalLink>? links = null)
    {
        var artist1 = Resource("artists", "a1", Attributes(name: "Metallica"));
        var artist2 = Resource("artists", "a2", Attributes(name: "Michael Kamen"));
        var artwork = Resource("artworks", "art1", Attributes(files: [File(1280), File(320), File(80)]));
        var album = Resource("albums", "al1", Attributes(title: "Metallica"), ("coverArt", Reference("artworks", "art1")));
        var track = Resource(
            "tracks",
            "109813974",
            Attributes(
                title: "Nothing Else Matters",
                version: version,
                isrc: "USEE19200002",
                externalLinks: links ?? [SharingLink("https://tidal.com/browse/track/109813974")]),
            ("artists", Reference("artists", "a1", "a2")),
            ("albums", Reference("albums", "al1")));

        return (track, Index(artist1, artist2, artwork, album));
    }

    [Fact]
    public void ToTrackResult_FullTrack_MapsEveryField()
    {
        var (track, included) = NewTrack();

        var result = TidalStreamingProvider.ToTrackResult(track, included);

        result.Type.Should().Be(StreamingResultType.Track);
        result.Name.Should().Be("Nothing Else Matters");
        result.ArtistName.Should().Be("Metallica, Michael Kamen");
        result.ImageUrl.Should().Be(new Uri("https://resources.example.com/320x320.jpg"));
        result.Isrc.Should().Be("USEE19200002");
        result.Upc.Should().BeNull();
        result.ReleaseType.Should().BeNull();
        result.Links.Should().Equal(
            new ProviderLinkCandidate(PlatformCodes.Tidal, "109813974", new Uri("https://tidal.com/browse/track/109813974")));
    }

    [Fact]
    public void ToTrackResult_VersionPresent_AppendsItToTheName()
    {
        var (track, included) = NewTrack(version: "Live");

        TidalStreamingProvider.ToTrackResult(track, included).Name.Should().Be("Nothing Else Matters (Live)");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToTrackResult_BlankVersion_LeavesTheNameAlone(string? version)
    {
        var (track, included) = NewTrack(version: version);

        TidalStreamingProvider.ToTrackResult(track, included).Name.Should().Be("Nothing Else Matters");
    }

    [Fact]
    public void ToTrackResult_NoSharingLink_BuildsTheUrlFromTheId()
    {
        var (track, included) = NewTrack(links: []);

        TidalStreamingProvider.ToTrackResult(track, included).Links.Single().ExternalUrl
            .Should().Be(new Uri("https://tidal.com/browse/track/109813974"));
    }

    [Fact]
    public void ToTrackResult_ArtistReferenceMissingFromIncluded_FallsBackToUnknown()
    {
        var (track, _) = NewTrack();

        var result = TidalStreamingProvider.ToTrackResult(track, Index());

        result.ArtistName.Should().Be("Unknown");
        result.ImageUrl.Should().BeNull();
    }

    [Fact]
    public void ToTrackResult_AlbumWithoutCoverArt_HasNoImage()
    {
        var album = Resource("albums", "al1", Attributes(title: "Metallica"));
        var track = Resource("tracks", "1", Attributes(title: "Song"), ("albums", Reference("albums", "al1")));

        TidalStreamingProvider.ToTrackResult(track, Index(album)).ImageUrl.Should().BeNull();
    }

    [Fact]
    public void ToAlbumResult_MapsUpcArtistsImageAndReleaseType()
    {
        var artist = Resource("artists", "a1", Attributes(name: "AURORA"));
        var artwork = Resource("artworks", "art1", Attributes(files: [File(1280), File(320)]));
        var album = Resource(
            "albums",
            "11764671",
            Attributes(
                title: "Infections of a Different Kind (Step I)",
                barcodeId: "0044003199682",
                albumType: "SINGLE",
                externalLinks: [SharingLink("https://tidal.com/browse/album/11764671")]),
            ("artists", Reference("artists", "a1")),
            ("coverArt", Reference("artworks", "art1")));

        var result = TidalStreamingProvider.ToAlbumResult(album, Index(artist, artwork));

        result.Type.Should().Be(StreamingResultType.Release);
        result.Name.Should().Be("Infections of a Different Kind (Step I)");
        result.ArtistName.Should().Be("AURORA");
        result.ImageUrl.Should().Be(new Uri("https://resources.example.com/320x320.jpg"));
        result.Upc.Should().Be("0044003199682");
        result.Isrc.Should().BeNull();
        result.ReleaseType.Should().Be(ReleaseType.Single);
        result.Links.Should().Equal(
            new ProviderLinkCandidate(PlatformCodes.Tidal, "11764671", new Uri("https://tidal.com/browse/album/11764671")));
    }

    [Theory]
    [InlineData("ALBUM", ReleaseType.Album)]
    [InlineData("SINGLE", ReleaseType.Single)]
    [InlineData("single", ReleaseType.Single)]
    [InlineData("EP", ReleaseType.EP)]
    [InlineData("SOMETHING_NEW", ReleaseType.Album)]
    [InlineData(null, ReleaseType.Album)]
    public void ToReleaseType_MapsTidalAlbumType(string? albumType, ReleaseType expected) =>
        TidalStreamingProvider.ToReleaseType(albumType).Should().Be(expected);

    [Fact]
    public void JoinArtists_SkipsBlankNamesAndFallsBackToUnknown()
    {
        var blank = Resource("artists", "a1", Attributes(name: " "));

        TidalStreamingProvider.JoinArtists([blank]).Should().Be("Unknown");
        TidalStreamingProvider.JoinArtists([]).Should().Be("Unknown");
    }

    [Fact]
    public void PickImage_PrefersTheSmallestFileAtLeast300Wide() =>
        TidalStreamingProvider.PickImage([File(1280), File(640), File(320), File(160)])
            .Should().Be(new Uri("https://resources.example.com/320x320.jpg"));

    [Fact]
    public void PickImage_AllFilesBelow300_PicksTheLargest() =>
        TidalStreamingProvider.PickImage([File(80), File(160)])
            .Should().Be(new Uri("https://resources.example.com/160x160.jpg"));

    [Fact]
    public void PickImage_NoUsableFiles_ReturnsNull()
    {
        TidalStreamingProvider.PickImage(null).Should().BeNull();
        TidalStreamingProvider.PickImage([]).Should().BeNull();
        TidalStreamingProvider.PickImage([new TidalFile("not a url", new TidalFileMeta(320, 320)), new TidalFile(null, null)]).Should().BeNull();
    }

    [Fact]
    public void BuildUrl_IgnoresLinksThatAreNotTheSharingLinkOrNotAbsolute()
    {
        TidalExternalLink[] links =
        [
            new("https://example.com/other", new TidalLinkMeta("OTHER")),
            new("/browse/album/5", new TidalLinkMeta("TIDAL_SHARING")),
        ];

        TidalStreamingProvider.BuildUrl(links, "album", "5").Should().Be(new Uri("https://tidal.com/browse/album/5"));
    }

    [Theory]
    [InlineData("1", "Title", true)]
    [InlineData(null, "Title", false)]
    [InlineData(" ", "Title", false)]
    [InlineData("1", null, false)]
    [InlineData("1", " ", false)]
    public void IsUsable_NeedsIdAndTitle(string? id, string? title, bool expected) =>
        TidalStreamingProvider.IsUsable(new TidalResource(id, "tracks", Attributes(title: title), null)).Should().Be(expected);

    [Fact]
    public void IndexIncluded_SkipsResourcesWithoutTypeOrId()
    {
        var index = TidalStreamingProvider.IndexIncluded(
        [
            new TidalResource("1", "tracks", null, null),
            new TidalResource(null, "tracks", null, null),
            new TidalResource("2", null, null, null),
        ]);

        index.Keys.Should().Equal(("tracks", "1"));
    }
}
