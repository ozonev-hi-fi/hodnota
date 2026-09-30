using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;

namespace Hodnota.Infrastructure.Tests.Catalog;

public class ShareEventHubTests
{
    private static readonly Guid PageId = Guid.NewGuid();

    private static IReadOnlyList<PlatformCheckResult> Result(string platformCode) => [new(platformCode, LookupOutcome.NotFound, null)];

    private static async Task<IReadOnlyList<PlatformCheckResult>> ReadOneAsync(IShareEventSubscription subscription)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var update in subscription.ReadAllAsync(timeout.Token))
        {
            return update;
        }

        throw new InvalidOperationException("The subscription ended without an update.");
    }

    [Fact]
    public async Task Publish_ReachesEverySubscriberOfThatPageOnly()
    {
        var hub = new ShareEventHub();
        using var first = hub.Subscribe(PageId);
        using var second = hub.Subscribe(PageId);
        using var otherPage = hub.Subscribe(Guid.NewGuid());

        hub.Publish(PageId, Result("tidal"));

        (await ReadOneAsync(first)).Single().PlatformCode.Should().Be("tidal");
        (await ReadOneAsync(second)).Single().PlatformCode.Should().Be("tidal");
        using var none = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        (await otherPage.ReadAllAsync(none.Token).ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Publish_AfterASubscriberIsDisposed_DoesNotReachIt()
    {
        var hub = new ShareEventHub();
        var subscription = hub.Subscribe(PageId);
        subscription.Dispose();

        hub.Publish(PageId, Result("tidal"));

        (await subscription.ReadAllAsync(CancellationToken.None).ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Subscribe_AfterTheLastSubscriberLeft_StillReceivesUpdates()
    {
        var hub = new ShareEventHub();
        hub.Subscribe(PageId).Dispose();

        using var later = hub.Subscribe(PageId);
        hub.Publish(PageId, Result("qobuz"));

        (await ReadOneAsync(later)).Single().PlatformCode.Should().Be("qobuz");
    }

    [Fact]
    public async Task SubscribeAndDisposeFromManyThreads_NeverLosesALiveSubscriber()
    {
        var hub = new ShareEventHub();
        using var watcher = hub.Subscribe(PageId);

        await Parallel.ForAsync(0, 500, async (_, _) =>
        {
            using var transient = hub.Subscribe(PageId);
            await Task.Yield();
        });
        hub.Publish(PageId, Result("tidal"));

        (await ReadOneAsync(watcher)).Single().PlatformCode.Should().Be("tidal");
    }

    [Fact]
    public async Task ReadAllAsync_EndsWithoutThrowingWhenCancelled()
    {
        var hub = new ShareEventHub();
        using var subscription = hub.Subscribe(PageId);
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var updates = await subscription.ReadAllAsync(cancelled.Token).ToListAsync();

        updates.Should().BeEmpty();
    }
}
