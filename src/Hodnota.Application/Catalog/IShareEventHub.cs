namespace Hodnota.Application.Catalog;

public interface IShareEventHub
{
    void Publish(Guid sharePageId, IReadOnlyList<PlatformCheckResult> results);

    IShareEventSubscription Subscribe(Guid sharePageId);
}

public interface IShareEventSubscription : IDisposable
{
    // Ends when the token is cancelled or the subscription is disposed; never throws for either.
    IAsyncEnumerable<IReadOnlyList<PlatformCheckResult>> ReadAllAsync(CancellationToken cancellationToken);
}
