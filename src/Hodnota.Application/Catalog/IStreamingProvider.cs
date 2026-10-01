namespace Hodnota.Application.Catalog;

public interface IStreamingProvider
{
    string ProviderCode { get; }

    // The platforms whose links this provider produces (YouTube produces two).
    IReadOnlyList<string> LinkPlatformCodes { get; }

    bool Supports(StreamingResultType type);

    bool SupportsLookup(StreamingResultType type);

    Task<IReadOnlyList<StreamingSearchResult>> SearchAsync(string query, StreamingResultType type, CancellationToken cancellationToken);

    // Returns only items whose own ISRC/UPC equals one of key.Codes; the provider does that check,
    // because not every provider exposes the code on the item it returns (Discogs).
    Task<IReadOnlyList<StreamingSearchResult>> LookupAsync(StreamingLookupKey key, CancellationToken cancellationToken);
}
