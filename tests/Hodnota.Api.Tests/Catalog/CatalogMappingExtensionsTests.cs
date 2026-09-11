using AwesomeAssertions;

using Hodnota.Api.Catalog;
using Hodnota.Application.Catalog;
using Hodnota.Contracts.Catalog;

using DomainPlatformType = Hodnota.Domain.Catalog.PlatformType;

namespace Hodnota.Api.Tests.Catalog;

public class CatalogMappingExtensionsTests
{
    public static IEnumerable<object[]> AllDomainPlatformTypes() =>
        Enum.GetValues<DomainPlatformType>().Select(value => new object[] { value });

    [Theory]
    [MemberData(nameof(AllDomainPlatformTypes))]
    public void ToResponse_MapsEveryDomainPlatformTypeToAContractPlatformType(DomainPlatformType platformType)
    {
        var result = new SharePageResult(
            Guid.NewGuid(),
            StreamingResultType.Track,
            "Nothing Else Matters",
            "Metallica",
            [new SharePageLinkResult("youtube", new Uri("https://example.com"), platformType)]);

        var response = result.ToResponse();

        response.Links.Single().Type.ToString().Should().Be(platformType.ToString(),
            $"{nameof(DomainPlatformType)}.{platformType} must have a matching {nameof(PlatformType)} case in {nameof(CatalogMappingExtensions)}");
    }
}
