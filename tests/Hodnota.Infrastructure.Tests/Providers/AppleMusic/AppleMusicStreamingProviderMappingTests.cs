using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;
using Hodnota.Infrastructure.Providers.AppleMusic;

namespace Hodnota.Infrastructure.Tests.Providers.AppleMusic;

// AppleMusicStreamingProvider.SearchAsync needs the real HTTP client stack (see
// AppleMusicApiClientTests and AppleMusicStreamingProviderSearchTests), but the DTO ->
// StreamingSearchResult mapping (internal, see InternalsVisibleTo) is a plain pure function and is
// tested directly here.
public class AppleMusicStreamingProviderMappingTests
{
    private const string Country = "US";

    private static ITunesItem NewTrackItem(
        long? trackId = 1,
        string? trackName = "Enter Sandman (Remastered 2021)",
        string? artistName = "Metallica",
        string? trackViewUrl = "https://music.apple.com/us/album/enter-sandman/1572051816?i=1572051818&uo=4",
        string? artworkUrl100 = "https://is1-ssl.mzstatic.com/image/thumb/abc/100x100bb.jpg",
        bool? isStreamable = true) =>
        new(null, "song", trackId, null, artistName, trackName, null, null, trackViewUrl, null, artworkUrl100, isStreamable);

    private static ITunesItem NewAlbumItem(
        long? collectionId = 1,
        string? collectionName = "Load (Remastered)",
        string? artistName = "Metallica",
        string? collectionViewUrl = "https://music.apple.com/us/album/load-remastered/1806720489?uo=4",
        string? artworkUrl100 = "https://is1-ssl.mzstatic.com/image/thumb/abc/100x100bb.jpg") =>
        new(null, null, null, collectionId, artistName, null, collectionName, "Album", null, collectionViewUrl, artworkUrl100, null);

    [Fact]
    public void ToTrackResult_MapsToTrackWithAppleMusicLink()
    {
        var result = AppleMusicStreamingProvider.ToTrackResult(NewTrackItem(), Country);

        result.Type.Should().Be(StreamingResultType.Track);
        result.Name.Should().Be("Enter Sandman (Remastered 2021)");
        result.ArtistName.Should().Be("Metallica");
        result.Isrc.Should().BeNull();
        result.Links.Should().BeEquivalentTo(
        [
            new ProviderLinkCandidate(PlatformCodes.AppleMusic, "1", new Uri("https://music.apple.com/us/album/enter-sandman/1572051816?i=1572051818")),
        ]);
    }

    [Fact]
    public void ToTrackResult_StripsOnlyTheReferralTrackingParameter()
    {
        var result = AppleMusicStreamingProvider.ToTrackResult(NewTrackItem(trackViewUrl: "https://music.apple.com/us/album/x/1?i=2&uo=4"), Country);

        result.Links.Single().ExternalUrl.Query.Should().Be("?i=2");
    }

    [Fact]
    public void ToTrackResult_MissingArtist_ArtistNameIsUnknown()
    {
        var result = AppleMusicStreamingProvider.ToTrackResult(NewTrackItem(artistName: null), Country);

        result.ArtistName.Should().Be("Unknown");
    }

    [Fact]
    public void ToTrackResult_MissingLink_FallsBackToBuiltUrl()
    {
        var result = AppleMusicStreamingProvider.ToTrackResult(NewTrackItem(trackViewUrl: null), Country);

        result.Links.Single().ExternalUrl.Should().Be(new Uri("https://music.apple.com/US/song/1"));
    }

    [Fact]
    public void ToAlbumResult_MapsToReleaseWithAppleMusicLinkAndNoUpc()
    {
        var result = AppleMusicStreamingProvider.ToAlbumResult(NewAlbumItem(), Country);

        result.Type.Should().Be(StreamingResultType.Release);
        result.Name.Should().Be("Load (Remastered)");
        result.ArtistName.Should().Be("Metallica");
        result.Upc.Should().BeNull();
        result.ReleaseType.Should().Be(ReleaseType.Album);
        result.Links.Should().BeEquivalentTo(
        [
            new ProviderLinkCandidate(PlatformCodes.AppleMusic, "1", new Uri("https://music.apple.com/us/album/load-remastered/1806720489")),
        ]);
    }

    [Fact]
    public void ToAlbumResult_SongDerivedUrl_DropsTheTrackQueryParameter()
    {
        var result = AppleMusicStreamingProvider.ToAlbumResult(
            NewAlbumItem(collectionViewUrl: "https://music.apple.com/us/album/wallflowers/1569867454?i=1569868216&uo=4"), Country);

        result.Links.Single().ExternalUrl.Should().Be(new Uri("https://music.apple.com/us/album/wallflowers/1569867454"));
    }

    [Fact]
    public void ToAlbumResult_MissingArtist_ArtistNameIsUnknown()
    {
        var result = AppleMusicStreamingProvider.ToAlbumResult(NewAlbumItem(artistName: null), Country);

        result.ArtistName.Should().Be("Unknown");
    }

    [Fact]
    public void ToAlbumResult_MissingLink_FallsBackToBuiltUrl()
    {
        var result = AppleMusicStreamingProvider.ToAlbumResult(NewAlbumItem(collectionViewUrl: null), Country);

        result.Links.Single().ExternalUrl.Should().Be(new Uri("https://music.apple.com/US/album/1"));
    }

    [Theory]
    [InlineData("Kafka - Single", "Kafka", ReleaseType.Single)]
    [InlineData("Micro - EP", "Micro", ReleaseType.EP)]
    [InlineData("Load (Remastered)", "Load (Remastered)", ReleaseType.Album)]
    public void ParseAlbumName_SplitsTheITunesTitleSuffix(string collectionName, string expectedName, ReleaseType expectedType)
    {
        var (name, releaseType) = AppleMusicStreamingProvider.ParseAlbumName(collectionName);

        name.Should().Be(expectedName);
        releaseType.Should().Be(expectedType);
    }

    [Fact]
    public void PickImage_Artwork100_UpsizesTo600()
    {
        AppleMusicStreamingProvider.PickImage("https://example.com/cover/100x100bb.jpg")
            .Should().Be(new Uri("https://example.com/cover/600x600bb.jpg"));
    }

    [Fact]
    public void PickImage_NoSizeMarker_KeepsTheOriginalUrl()
    {
        AppleMusicStreamingProvider.PickImage("https://example.com/cover.jpg")
            .Should().Be(new Uri("https://example.com/cover.jpg"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PickImage_NoArtwork_ReturnsNull(string? artworkUrl100)
    {
        AppleMusicStreamingProvider.PickImage(artworkUrl100).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsUsableTrack_BlankTrackName_ReturnsFalse(string? trackName)
    {
        AppleMusicStreamingProvider.IsUsableTrack(NewTrackItem(trackName: trackName)).Should().BeFalse();
    }

    [Fact]
    public void IsUsableTrack_MissingTrackId_ReturnsFalse()
    {
        AppleMusicStreamingProvider.IsUsableTrack(NewTrackItem(trackId: null)).Should().BeFalse();
    }

    [Fact]
    public void IsUsableTrack_UsableTrack_ReturnsTrue()
    {
        AppleMusicStreamingProvider.IsUsableTrack(NewTrackItem()).Should().BeTrue();
    }

    [Fact]
    public void IsUsableTrack_PurchaseOnlyTrack_StillUsable()
    {
        AppleMusicStreamingProvider.IsUsableTrack(NewTrackItem(isStreamable: false)).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsUsableAlbum_BlankCollectionName_ReturnsFalse(string? collectionName)
    {
        AppleMusicStreamingProvider.IsUsableAlbum(NewAlbumItem(collectionName: collectionName)).Should().BeFalse();
    }

    [Fact]
    public void IsUsableAlbum_MissingCollectionId_ReturnsFalse()
    {
        AppleMusicStreamingProvider.IsUsableAlbum(NewAlbumItem(collectionId: null)).Should().BeFalse();
    }

    [Fact]
    public void IsUsableAlbum_UsableAlbum_ReturnsTrue()
    {
        AppleMusicStreamingProvider.IsUsableAlbum(NewAlbumItem()).Should().BeTrue();
    }

    [Fact]
    public void ProviderCode_IsAppleMusic()
    {
        new AppleMusicStreamingProvider(null!, new AppleMusicSettings(Country)).ProviderCode.Should().Be(ProviderCodes.AppleMusic);
    }

    [Fact]
    public void SupportsLookup_BothTypes_ReturnsFalse()
    {
        var provider = new AppleMusicStreamingProvider(null!, new AppleMusicSettings(Country));

        provider.Supports(StreamingResultType.Track).Should().BeTrue();
        provider.Supports(StreamingResultType.Release).Should().BeTrue();
        provider.SupportsLookup(StreamingResultType.Track).Should().BeFalse();
        provider.SupportsLookup(StreamingResultType.Release).Should().BeFalse();
        provider.LinkPlatformCodes.Should().Equal(PlatformCodes.AppleMusic);
    }

    [Fact]
    public async Task LookupAsync_ThrowsNotSupported()
    {
        var provider = new AppleMusicStreamingProvider(null!, new AppleMusicSettings(Country));

        var act = () => provider.LookupAsync(new StreamingLookupKey(StreamingResultType.Track, ["ABC"]), CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
    }
}
