using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hodnota.Infrastructure.Tests.Catalog;

// Exercises RunJobAsync's failure paths end to end: a viewer watching the share page must see every
// queued platform settle, even when a save throws or the whole job fails, not wait for the watch
// endpoint's own 60 s timeout. The 4-job concurrency limit is not covered here — a timing-based test
// of it would be flaky.
public class EnrichmentWorkerTests
{
    private static readonly Guid PageId = Guid.NewGuid();

    private static IStreamingProvider NewProvider(string providerCode, params string[] platformCodes)
    {
        var provider = Substitute.For<IStreamingProvider>();
        provider.ProviderCode.Returns(providerCode);
        provider.LinkPlatformCodes.Returns(platformCodes.Length > 0 ? platformCodes : [providerCode]);
        provider.Supports(Arg.Any<StreamingResultType>()).Returns(true);
        provider.SupportsLookup(Arg.Any<StreamingResultType>()).Returns(true);
        provider.LookupAsync(Arg.Any<StreamingLookupKey>(), Arg.Any<CancellationToken>()).Returns([]);
        return provider;
    }

    private static EnrichmentRequest Request() => new(StreamingResultType.Track, "Song", "Artist", "USRC17607839", null, []);

    private static (EnrichmentWorker Worker, InProcessEnrichmentQueue Queue, ShareEventHub Hub, ServiceProvider Services) NewWorker(
        ICatalogRepository repository, params IStreamingProvider[] providers)
    {
        var services = new ServiceCollection();
        services.AddSingleton(repository);
        services.AddSingleton<ILogger<CatalogEnrichmentService>>(NullLogger<CatalogEnrichmentService>.Instance);
        foreach (var provider in providers)
        {
            services.AddSingleton(provider);
        }

        services.AddScoped<CatalogEnrichmentService>();
        var serviceProvider = services.BuildServiceProvider();

        var queue = new InProcessEnrichmentQueue();
        var hub = new ShareEventHub();
        var worker = new EnrichmentWorker(queue, hub, serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<EnrichmentWorker>.Instance);
        return (worker, queue, hub, serviceProvider);
    }

    private static async Task<IReadOnlyList<PlatformCheckResult>> ReadOneAsync(IShareEventSubscription subscription)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var update in subscription.ReadAllAsync(timeout.Token))
        {
            return update;
        }

        throw new InvalidOperationException("The subscription ended without an update.");
    }

    private static async Task WaitUntilNotActiveAsync(InProcessEnrichmentQueue queue, string providerCode)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (queue.IsActive(PageId, providerCode))
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, CancellationToken.None);
        }
    }

    [Fact]
    public async Task SaveSucceeds_PublishesTheSavedRowsAndReleasesTheProvider()
    {
        var repository = Substitute.For<ICatalogRepository>();
        repository.GetEnrichmentRequestAsync(PageId, Arg.Any<CancellationToken>()).Returns(Request());
        IReadOnlyList<PlatformCheckResult> saved = [new("tidal", LookupOutcome.NotFound)];
        repository.SaveEnrichmentAsync(PageId, Arg.Any<ProviderEnrichment>(), Arg.Any<CancellationToken>()).Returns(saved);
        var (worker, queue, hub, services) = NewWorker(repository, NewProvider("tidal"));
        await using var _ = services;
        var hostedService = (IHostedService)worker;
        await hostedService.StartAsync(CancellationToken.None);
        try
        {
            using var subscription = hub.Subscribe(PageId);
            queue.Enqueue(PageId, ["tidal"]);

            var published = await ReadOneAsync(subscription);

            published.Should().BeEquivalentTo(saved);
            await WaitUntilNotActiveAsync(queue, "tidal");
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task SaveThrows_RecordsAndPublishesAFailedCheck()
    {
        var repository = Substitute.For<ICatalogRepository>();
        repository.GetEnrichmentRequestAsync(PageId, Arg.Any<CancellationToken>()).Returns(Request());
        IReadOnlyList<PlatformCheckResult> failedSave = [new("tidal", LookupOutcome.Failed)];
        repository.SaveEnrichmentAsync(PageId, Arg.Any<ProviderEnrichment>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new InvalidOperationException("Database unavailable."),
                _ => failedSave);
        var (worker, queue, hub, services) = NewWorker(repository, NewProvider("tidal"));
        await using var _ = services;
        var hostedService = (IHostedService)worker;
        await hostedService.StartAsync(CancellationToken.None);
        try
        {
            using var subscription = hub.Subscribe(PageId);
            queue.Enqueue(PageId, ["tidal"]);

            var published = await ReadOneAsync(subscription);

            published.Should().BeEquivalentTo(failedSave);
            await repository.Received(2).SaveEnrichmentAsync(PageId, Arg.Any<ProviderEnrichment>(), Arg.Any<CancellationToken>());
            await WaitUntilNotActiveAsync(queue, "tidal");
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task SaveThrowsTwice_PublishesAnInMemoryFailedCheckForEveryPlatformOfTheProvider()
    {
        var repository = Substitute.For<ICatalogRepository>();
        repository.GetEnrichmentRequestAsync(PageId, Arg.Any<CancellationToken>()).Returns(Request());
        repository.SaveEnrichmentAsync(PageId, Arg.Any<ProviderEnrichment>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Database unavailable."));
        // A provider with two platforms (like YouTube) must have both settle, not just the one checked.
        var (worker, queue, hub, services) = NewWorker(repository, NewProvider("youtube", "youtube", "youtube-music"));
        await using var _ = services;
        var hostedService = (IHostedService)worker;
        await hostedService.StartAsync(CancellationToken.None);
        try
        {
            using var subscription = hub.Subscribe(PageId);
            queue.Enqueue(PageId, ["youtube"]);

            var published = await ReadOneAsync(subscription);

            published.Should().BeEquivalentTo<PlatformCheckResult>(
            [
                new("youtube", LookupOutcome.Failed),
                new("youtube-music", LookupOutcome.Failed),
            ]);
            await WaitUntilNotActiveAsync(queue, "youtube");
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task GetEnrichmentRequestThrows_PublishesAnInMemoryFailedCheckForEveryQueuedProvider()
    {
        var repository = Substitute.For<ICatalogRepository>();
        repository.GetEnrichmentRequestAsync(PageId, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("Database unavailable."));
        var (worker, queue, hub, services) = NewWorker(repository, NewProvider("tidal"));
        await using var _ = services;
        var hostedService = (IHostedService)worker;
        await hostedService.StartAsync(CancellationToken.None);
        try
        {
            using var subscription = hub.Subscribe(PageId);
            queue.Enqueue(PageId, ["tidal"]);

            var published = await ReadOneAsync(subscription);

            published.Should().BeEquivalentTo<PlatformCheckResult>([new("tidal", LookupOutcome.Failed)]);
            await repository.DidNotReceiveWithAnyArgs().SaveEnrichmentAsync(default, default!, default);
            await WaitUntilNotActiveAsync(queue, "tidal");
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task PageNoLongerExists_PublishesNothingAndReleasesTheProvider()
    {
        var repository = Substitute.For<ICatalogRepository>();
        repository.GetEnrichmentRequestAsync(PageId, Arg.Any<CancellationToken>()).Returns((EnrichmentRequest?)null);
        var (worker, queue, hub, services) = NewWorker(repository, NewProvider("tidal"));
        await using var _ = services;
        var hostedService = (IHostedService)worker;
        await hostedService.StartAsync(CancellationToken.None);
        try
        {
            using var subscription = hub.Subscribe(PageId);
            queue.Enqueue(PageId, ["tidal"]);
            await WaitUntilNotActiveAsync(queue, "tidal");

            using var noMoreUpdates = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            (await subscription.ReadAllAsync(noMoreUpdates.Token).ToListAsync()).Should().BeEmpty();
            await repository.DidNotReceiveWithAnyArgs().SaveEnrichmentAsync(default, default!, default);
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }
    }
}
