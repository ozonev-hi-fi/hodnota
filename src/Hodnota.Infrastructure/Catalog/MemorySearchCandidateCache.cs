using Hodnota.Application.Catalog;

using Microsoft.Extensions.Caching.Memory;

namespace Hodnota.Infrastructure.Catalog;

public sealed class MemorySearchCandidateCache(IMemoryCache cache) : ISearchCandidateCache
{
    // Bounds the cache's own entry count (via the dedicated instance's SizeLimit this is registered
    // with) rather than relying on the 15-minute Ttl alone, which only evicts on access/compaction and
    // would otherwise let a traffic burst grow this unbounded until then.
    public const int MaxEntries = 10_000;

    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    public string Store(StreamingSearchResult result)
    {
        var candidateId = Guid.NewGuid().ToString("N");
        cache.Set(candidateId, result, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl, Size = 1 });
        return candidateId;
    }

    public StreamingSearchResult? Get(string candidateId) => cache.Get<StreamingSearchResult>(candidateId);
}
