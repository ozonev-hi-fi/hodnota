using AwesomeAssertions;

using Hodnota.Application.Catalog;

namespace Hodnota.Application.Tests.Catalog;

// The titles and channels are real YouTube playlist results (2026-10-01) for the albums "Load" by
// "Metallica" and "Ecce Lex" by "Nostromo".
public class SearchResultNameMatcherTests
{
    private static StreamingSearchResult Playlist(string title, string channel) => new(
        StreamingResultType.Release,
        title,
        channel,
        null,
        [new ProviderLinkCandidate("youtube", "PL1", new Uri("https://www.youtube.com/playlist?list=PL1"))]);

    [Theory]
    [InlineData("Load", "Metallica")]
    [InlineData("Load (Remastered)", "Metallica - Topic")]
    [InlineData("Metallica - Load (Full Album)", "Metallica")]
    [InlineData("Load - Metallica", "MetallicaVEVO")]
    public void IsSameItem_TheUploaderIsTheArtistAndTheTitleIsTheAlbumTitle_IsTrue(string title, string channel) =>
        SearchResultNameMatcher.IsSameItem(Playlist(title, channel), "Metallica", "Load").Should().BeTrue();

    [Theory]
    [InlineData("Metallica · Load (Full Album)", "ALAN&PABLO")]
    [InlineData("Metallica Load", "BTLoffical")]
    [InlineData("Metallica - Load (Remastered)", "Lucas Gonçalves")]
    [InlineData("Load", "Some Fan")]
    public void IsSameItem_AnyoneElseUploadedItWithAPerfectTitle_IsFalse(string title, string channel) =>
        SearchResultNameMatcher.IsSameItem(Playlist(title, channel), "Metallica", "Load").Should().BeFalse();

    [Fact]
    public void IsSameItem_NostromoPirateUpload_IsFalse() =>
        SearchResultNameMatcher.IsSameItem(Playlist("Nostromo - Ecce Lex (Full Album)", "somuchdeath18"), "Nostromo", "Ecce Lex").Should().BeFalse();

    [Theory]
    [InlineData("Load: Lyric Videos", "Metallica")]
    [InlineData("Get the Load Out", "Metallica")]
    [InlineData("Metallica - Load/Reload", "Metallica")]
    [InlineData("Reload", "Metallica - Topic")]
    public void IsSameItem_TheArtistsOwnChannelButAnotherTitle_IsFalse(string title, string channel) =>
        SearchResultNameMatcher.IsSameItem(Playlist(title, channel), "Metallica", "Load").Should().BeFalse();

    [Fact]
    public void IsSameItem_AnotherArtistsChannelWithTheSameAlbumTitle_IsFalse() =>
        SearchResultNameMatcher.IsSameItem(Playlist("Load", "Some Band"), "Metallica", "Load").Should().BeFalse();

    [Fact]
    public void IsSameItem_NoArtistToProve_IsFalse() =>
        SearchResultNameMatcher.IsSameItem(Playlist("Load", "Metallica"), string.Empty, "Load").Should().BeFalse();
}
