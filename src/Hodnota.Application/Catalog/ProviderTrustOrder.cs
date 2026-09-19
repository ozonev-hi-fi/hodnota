namespace Hodnota.Application.Catalog;

public static class ProviderTrustOrder
{
    private static readonly string[] Order = [ProviderCodes.Spotify, ProviderCodes.YouTube];

    public static int RankOf(string providerCode)
    {
        var index = Array.IndexOf(Order, providerCode);
        return index < 0 ? int.MaxValue : index;
    }

    public static IReadOnlyList<IStreamingProvider> Sort(IEnumerable<IStreamingProvider> providers) =>
        [.. providers.OrderBy(provider => RankOf(provider.ProviderCode))];
}
