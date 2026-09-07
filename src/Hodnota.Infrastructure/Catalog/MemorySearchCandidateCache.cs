using Hodnota.Application.Catalog;

using Microsoft.Extensions.Caching.Memory;

namespace Hodnota.Infrastructure.Catalog;

public sealed class MemorySearchCandidateCache(IMemoryCache cache) : ISearchCandidateCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    public string Store(StreamingSearchResult result)
    {
        var candidateId = Guid.NewGuid().ToString("N");
        cache.Set(candidateId, result, Ttl);
        return candidateId;
    }

    public StreamingSearchResult? Get(string candidateId) => cache.Get<StreamingSearchResult>(candidateId);
}
