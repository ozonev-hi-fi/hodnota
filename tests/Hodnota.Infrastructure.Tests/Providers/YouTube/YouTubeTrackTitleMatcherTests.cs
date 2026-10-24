using AwesomeAssertions;

using Hodnota.Infrastructure.Providers.YouTube;

namespace Hodnota.Infrastructure.Tests.Providers.YouTube;

public class YouTubeTrackTitleMatcherTests
{
    [Theory]
    [InlineData("Rude Awakening (Stripped)", "Rude Awakening", true)]
    [InlineData("Until It Sleeps - Live", "Until It Sleeps", false)]
    [InlineData("Nothing Else Matters - Remix", "Nothing Else Matters", false)]
    [InlineData("King Nothing (Rough Mix) [Remastered]", "King Nothing", true)]
    [InlineData("One (Remastered)", "One", true)]
    [InlineData("Ain’t My Bitch", "Ain't My Bitch", true)]
    [InlineData("Alone", "One", false)]
    [InlineData("Someone", "One", false)]
    [InlineData("One More Time", "One", false)]
    [InlineData("Secure", "Cure", false)]
    [InlineData("Reload", "Load", false)]
    [InlineData("(Intro)", "Intro", false)]
    [InlineData(null, "One", false)]
    public void IsAnyKnownTrack_ComparesWholeTitlesIgnoringATrailingVariant(string? uploadedTitle, string knownTitle, bool expected) =>
        YouTubeTrackTitleMatcher.IsAnyKnownTrack(uploadedTitle, [knownTitle]).Should().Be(expected);

    [Fact]
    public void IsAnyKnownTrack_NoKnownTitles_IsFalse() =>
        YouTubeTrackTitleMatcher.IsAnyKnownTrack("One", []).Should().BeFalse();
}
