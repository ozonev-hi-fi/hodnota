using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Hodnota.Application.Catalog;

namespace Hodnota.Infrastructure.Catalog;

// In memory, so it reaches only viewers connected to this server instance — the same limit as the
// search candidate cache.
public sealed class ShareEventHub : IShareEventHub
{
    private readonly Dictionary<Guid, Dictionary<Guid, Channel<IReadOnlyList<PlatformCheckResult>>>> _subscriptions = [];
    private readonly Lock _gate = new();

    public void Publish(Guid sharePageId, IReadOnlyList<PlatformCheckResult> results)
    {
        List<Channel<IReadOnlyList<PlatformCheckResult>>> channels;
        lock (_gate)
        {
            channels = _subscriptions.TryGetValue(sharePageId, out var subscribers) ? [.. subscribers.Values] : [];
        }

        foreach (var channel in channels)
        {
            channel.Writer.TryWrite(results);
        }
    }

    public IShareEventSubscription Subscribe(Guid sharePageId)
    {
        var channel = Channel.CreateUnbounded<IReadOnlyList<PlatformCheckResult>>();
        var subscriptionId = Guid.NewGuid();

        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(sharePageId, out var subscribers))
            {
                subscribers = [];
                _subscriptions.Add(sharePageId, subscribers);
            }

            subscribers.Add(subscriptionId, channel);
        }

        return new Subscription(channel, () => Remove(sharePageId, subscriptionId));
    }

    private void Remove(Guid sharePageId, Guid subscriptionId)
    {
        lock (_gate)
        {
            if (_subscriptions.TryGetValue(sharePageId, out var subscribers) && subscribers.Remove(subscriptionId) && subscribers.Count == 0)
            {
                _subscriptions.Remove(sharePageId);
            }
        }
    }

    private sealed class Subscription(Channel<IReadOnlyList<PlatformCheckResult>> channel, Action onDispose) : IShareEventSubscription
    {
        public async IAsyncEnumerable<IReadOnlyList<PlatformCheckResult>> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (true)
            {
                bool canRead;
                try
                {
                    canRead = await channel.Reader.WaitToReadAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    canRead = false;
                }

                if (!canRead)
                {
                    yield break;
                }

                while (channel.Reader.TryRead(out var item))
                {
                    yield return item;
                }
            }
        }

        public void Dispose()
        {
            channel.Writer.TryComplete();
            onDispose();
        }
    }
}
