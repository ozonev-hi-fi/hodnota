using AwesomeAssertions;

using Hodnota.Api.Catalog;
using Hodnota.Application.Catalog;
using Hodnota.Contracts.Catalog;

using ContractRowState = Hodnota.Contracts.Catalog.PlatformRowState;
using DomainPlatformType = Hodnota.Domain.Catalog.PlatformType;

namespace Hodnota.Api.Tests.Catalog;

public class CatalogMappingExtensionsTests
{
    public static IEnumerable<object[]> AllDomainPlatformTypes() =>
        Enum.GetValues<DomainPlatformType>().Select(value => new object[] { value });

    [Theory]
    [MemberData(nameof(AllDomainPlatformTypes))]
    public void ToResponse_Row_MapsEveryDomainPlatformTypeToAContractPlatformType(DomainPlatformType platformType)
    {
        var row = new PlatformRow("youtube", platformType, Application.Catalog.PlatformRowState.Found, new Uri("https://example.com"));

        var response = row.ToResponse();

        response.Type.ToString().Should().Be(platformType.ToString(),
            $"{nameof(DomainPlatformType)}.{platformType} must have a matching {nameof(PlatformType)} case in {nameof(CatalogMappingExtensions)}");
    }

    public static IEnumerable<object[]> AllRowStates() =>
        Enum.GetValues<Application.Catalog.PlatformRowState>().Select(value => new object[] { value });

    [Theory]
    [MemberData(nameof(AllRowStates))]
    public void ToResponse_Row_MapsEveryRowStateToAContractRowState(Application.Catalog.PlatformRowState state)
    {
        var row = new PlatformRow("tidal", DomainPlatformType.StreamingService, state, new Uri("https://tidal.com/browse/track/1"));

        var response = row.ToResponse();

        response.State.ToString().Should().Be(state.ToString());
        response.Platform.Should().Be("tidal");
        response.Type.Should().Be(PlatformType.StreamingService);
        response.Url.Should().Be(new Uri("https://tidal.com/browse/track/1"));
    }

    [Fact]
    public void ToResponse_View_CarriesTheRowsAndTheCompleteFlag()
    {
        var page = new SharePageResult(Guid.NewGuid(), StreamingResultType.Release, "OK Computer", "Radiohead", []);
        var view = new SharePageView(page, [new PlatformRow("discogs", DomainPlatformType.Database, Application.Catalog.PlatformRowState.Checking, null)], IsComplete: false);

        var response = view.ToResponse();

        response.Type.Should().Be(CandidateType.Album);
        response.IsComplete.Should().BeFalse();
        response.Platforms.Should().ContainSingle().Which.State.Should().Be(ContractRowState.Checking);
    }

    [Fact]
    public void ToResponse_Candidate_ListsPlatformCodesFromItsLinksInOrder()
    {
        var result = new StreamingSearchResult(
            StreamingResultType.Track,
            "Nothing Else Matters",
            "Metallica",
            null,
            [
                new ProviderLinkCandidate("spotify", "sp1", new Uri("https://open.spotify.com/track/sp1")),
                new ProviderLinkCandidate("youtube", "yt1", new Uri("https://www.youtube.com/watch?v=yt1")),
                new ProviderLinkCandidate("youtube-music", "yt1", new Uri("https://music.youtube.com/watch?v=yt1")),
            ]);
        var candidate = new CatalogSearchCandidate("candidate-1", result);

        var response = candidate.ToResponse();

        response.Platforms.Should().Equal("spotify", "youtube", "youtube-music");
    }
}
