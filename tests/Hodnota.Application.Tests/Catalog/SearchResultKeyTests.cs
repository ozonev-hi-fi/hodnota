using AwesomeAssertions;

using Hodnota.Application.Catalog;

namespace Hodnota.Application.Tests.Catalog;

public class SearchResultKeyTests
{
    private static StreamingSearchResult NewResult(string name, string artist, StreamingResultType type = StreamingResultType.Track) =>
        new(type, name, artist, null, []);

    [Theory]
    [InlineData("Nothing Else Matters", "Metallica")]
    [InlineData("Metallica - Nothing Else Matters (Official Music Video)", "Metallica")]
    [InlineData("Metallica – Nothing Else Matters [Remastered 2021]", "MetallicaVEVO")]
    [InlineData("Nothing Else Matters", "Metallica - Topic")]
    [InlineData("Nothing Else Matters - Remastered", "Metallica")]
    public void Build_SpotifyAndYouTubeVariantsOfTheSameTrack_ProduceEqualKeys(string name, string artist)
    {
        var key = SearchResultKey.Build(NewResult(name, artist));

        key.Should().Be(new SearchResultKey.MatchKey(StreamingResultType.Track, "metallica", "nothing else matters"));
    }

    [Fact]
    public void Build_TrackAndReleaseWithSameNameAndArtist_ProduceDifferentKeys()
    {
        var trackKey = SearchResultKey.Build(NewResult("Metallica", "Metallica", StreamingResultType.Track));
        var releaseKey = SearchResultKey.Build(NewResult("Metallica", "Metallica", StreamingResultType.Release));

        trackKey.Should().NotBe(releaseKey);
        trackKey.Artist.Should().Be(releaseKey.Artist);
        trackKey.Title.Should().Be(releaseKey.Title);
    }

    [Fact]
    public void Build_FullAlbumPlaylistTitle_MatchesTheSelfTitledAlbum()
    {
        var spineKey = SearchResultKey.Build(NewResult("Metallica", "Metallica", StreamingResultType.Release));
        var youTubeKey = SearchResultKey.Build(NewResult("Metallica - Metallica (Full Album)", "Metallica", StreamingResultType.Release));

        youTubeKey.Should().Be(spineKey);
    }

    [Fact]
    public void Build_TitlePrefixedWithArtistName_StripsThePrefix()
    {
        var key = SearchResultKey.Build(NewResult("Metallica - Nothing Else Matters", "Metallica"));

        key.Title.Should().Be("nothing else matters");
    }

    [Fact]
    public void Build_TitleSuffixedWithArtistName_StripsTheSuffix()
    {
        var key = SearchResultKey.Build(NewResult("Nothing Else Matters - Metallica", "Metallica"));

        key.Title.Should().Be("nothing else matters");
    }

    [Fact]
    public void Build_LiveVersusStudioOfSameSong_ProduceDifferentKeys()
    {
        var studioKey = SearchResultKey.Build(NewResult("Nothing Else Matters", "Metallica"));
        var liveKey = SearchResultKey.Build(NewResult("Nothing Else Matters (Live)", "Metallica - Topic"));

        liveKey.Should().NotBe(studioKey);
    }

    [Fact]
    public void Build_DifferentTitlesBothNormalizingToEmpty_ProduceDifferentKeys()
    {
        var firstKey = SearchResultKey.Build(NewResult("(Remastered)", "Metallica"));
        var secondKey = SearchResultKey.Build(NewResult("!!!", "Metallica"));

        firstKey.Should().NotBe(secondKey);
    }

    [Fact]
    public void Build_LabelChannelArtistName_DoesNotMatchTheRealArtist()
    {
        var spineKey = SearchResultKey.Build(NewResult("Nothing Else Matters", "Metallica"));
        var labelKey = SearchResultKey.Build(NewResult("Metallica - Nothing Else Matters", "Warner Records"));

        labelKey.Should().NotBe(spineKey);
    }

    [Fact]
    public void Normalize_StripsOfficialMusicVideoBracket()
    {
        SearchResultKey.Normalize("Song (Official Music Video)").Should().Be("song");
    }

    [Theory]
    [InlineData("Song (Remastered)")]
    [InlineData("Song (Remastered 2021)")]
    [InlineData("Song (2021 Remaster)")]
    public void Normalize_StripsRemasterNoiseWithAndWithoutYear(string value)
    {
        SearchResultKey.Normalize(value).Should().Be("song");
    }

    [Fact]
    public void Normalize_StripsTrailingDashSeparatedNoiseSegment()
    {
        SearchResultKey.Normalize("Nothing Else Matters - Remastered").Should().Be("nothing else matters");
    }

    [Theory]
    [InlineData("Song (Live)", "song live")]
    [InlineData("Song (Remix)", "song remix")]
    [InlineData("Song (Acoustic)", "song acoustic")]
    public void Normalize_KeepsLiveRemixAndAcousticMarkers(string value, string expected)
    {
        SearchResultKey.Normalize(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("Song (feat. Someone)")]
    [InlineData("Song ft. Someone")]
    [InlineData("Song featuring Someone")]
    public void Normalize_StripsFeaturedArtistSegment(string value)
    {
        SearchResultKey.Normalize(value).Should().Be("song");
    }

    [Fact]
    public void Normalize_StripsDiacriticsAndPunctuation()
    {
        SearchResultKey.Normalize("Björk!").Should().Be("bjork");
    }

    [Fact]
    public void Normalize_CollapsesRepeatedWhitespace()
    {
        SearchResultKey.Normalize("Song   Name").Should().Be("song name");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_BlankValue_ReturnsEmpty(string? value)
    {
        SearchResultKey.Normalize(value).Should().BeEmpty();
    }

    [Fact]
    public void NormalizeArtist_TopicChannelSuffix_IsStripped()
    {
        SearchResultKey.NormalizeArtist("Metallica - Topic").Should().Be("metallica");
    }

    [Theory]
    [InlineData("MetallicaVEVO")]
    [InlineData("Metallica VEVO")]
    public void NormalizeArtist_VevoSuffix_IsStripped(string artist)
    {
        SearchResultKey.NormalizeArtist(artist).Should().Be("metallica");
    }

    [Fact]
    public void NormalizeArtist_MultipleArtists_UsesOnlyTheFirst()
    {
        SearchResultKey.NormalizeArtist("Jay-Z, Alicia Keys").Should().Be("jay z");
    }

    [Fact]
    public void NormalizeArtist_AmpersandBandName_IsKeptWhole()
    {
        SearchResultKey.NormalizeArtist("Simon & Garfunkel").Should().Be("simon garfunkel");
    }
}
