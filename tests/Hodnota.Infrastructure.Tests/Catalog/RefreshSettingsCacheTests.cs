using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Catalog;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Hodnota.Infrastructure.Tests.Catalog;

public class RefreshSettingsCacheTests
{
    private static (RefreshSettingsCache Cache, ICatalogRepository Repository) NewCache()
    {
        var repository = Substitute.For<ICatalogRepository>();
        var services = new ServiceCollection();
        services.AddSingleton(repository);
        var serviceProvider = services.BuildServiceProvider();

        var cache = new RefreshSettingsCache(new MemoryCache(new MemoryCacheOptions()), serviceProvider.GetRequiredService<IServiceScopeFactory>());
        return (cache, repository);
    }

    [Fact]
    public async Task GetIntervalsAsync_FirstCall_ReadsFromTheRepository()
    {
        var (cache, repository) = NewCache();
        repository.GetRefreshIntervalsAsync(Arg.Any<CancellationToken>()).Returns(new RefreshIntervals(7, 90));

        var intervals = await cache.GetIntervalsAsync(CancellationToken.None);

        intervals.Should().Be(new RefreshIntervals(7, 90));
        await repository.Received(1).GetRefreshIntervalsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetIntervalsAsync_SecondCall_ReadsFromTheCacheNotTheRepository()
    {
        var (cache, repository) = NewCache();
        repository.GetRefreshIntervalsAsync(Arg.Any<CancellationToken>()).Returns(new RefreshIntervals(7, 90));

        await cache.GetIntervalsAsync(CancellationToken.None);
        await cache.GetIntervalsAsync(CancellationToken.None);

        await repository.Received(1).GetRefreshIntervalsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalidate_ThenGetIntervalsAsync_ReadsFromTheRepositoryAgain()
    {
        var (cache, repository) = NewCache();
        repository.GetRefreshIntervalsAsync(Arg.Any<CancellationToken>()).Returns(new RefreshIntervals(7, 90), new RefreshIntervals(3, 30));

        var first = await cache.GetIntervalsAsync(CancellationToken.None);
        cache.Invalidate();
        var second = await cache.GetIntervalsAsync(CancellationToken.None);

        first.Should().Be(new RefreshIntervals(7, 90));
        second.Should().Be(new RefreshIntervals(3, 30));
        await repository.Received(2).GetRefreshIntervalsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateDuringARead_TheReadValueIsNotCached()
    {
        var (cache, repository) = NewCache();
        var staleRead = new TaskCompletionSource<RefreshIntervals>();
        repository.GetRefreshIntervalsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => staleRead.Task, _ => Task.FromResult(new RefreshIntervals(3, 30)));

        var slowReader = cache.GetIntervalsAsync(CancellationToken.None);
        cache.Invalidate();
        staleRead.SetResult(new RefreshIntervals(7, 90));
        await slowReader;

        var next = await cache.GetIntervalsAsync(CancellationToken.None);

        next.Should().Be(new RefreshIntervals(3, 30));
    }
}
