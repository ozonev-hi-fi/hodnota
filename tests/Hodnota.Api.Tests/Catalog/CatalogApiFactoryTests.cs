using System.Reflection;

using AwesomeAssertions;

using Hodnota.Application.Catalog;

namespace Hodnota.Api.Tests.Catalog;

// Guards the test factory itself: a new ProviderCodes constant without a matching stub in
// AllProviders would leave that provider out of every endpoint test, including the
// "all providers fail" one, without anything failing.
public class CatalogApiFactoryTests
{
    [Fact]
    public void AllProviders_CoversEveryProviderCodeInTrustOrder()
    {
        using var factory = new CatalogApiFactory();
        var providerCodes = typeof(ProviderCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!);

        var stubCodes = factory.AllProviders.Select(stub => stub.ProviderCode).ToList();

        stubCodes.Should().BeEquivalentTo(providerCodes);
        stubCodes.Should().Equal(stubCodes.OrderBy(ProviderTrustOrder.RankOf));
    }
}
