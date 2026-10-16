using Hodnota.Application.Catalog;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Hodnota.Infrastructure.Catalog;

public sealed class RefreshSettingsCache(IMemoryCache cache, IServiceScopeFactory scopeFactory) : IRefreshSettingsCache
{
    private const string CacheKey = "refresh-settings";
    private static readonly TimeSpan SafetyNetTtl = TimeSpan.FromHours(1);

    private readonly Lock _gate = new();
    private long _version;

    public async Task<RefreshIntervals> GetIntervalsAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out RefreshIntervals? cached) && cached is not null)
        {
            return cached;
        }

        long versionBeforeRead;
        lock (_gate)
        {
            versionBeforeRead = _version;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICatalogRepository>();
        var intervals = await repository.GetRefreshIntervalsAsync(cancellationToken);

        // An Invalidate() during the read means this value may predate the write that caused it;
        // caching it would hide the new value until the TTL runs out.
        lock (_gate)
        {
            if (_version == versionBeforeRead)
            {
                cache.Set(CacheKey, intervals, SafetyNetTtl);
            }
        }

        return intervals;
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _version++;
            cache.Remove(CacheKey);
        }
    }
}
