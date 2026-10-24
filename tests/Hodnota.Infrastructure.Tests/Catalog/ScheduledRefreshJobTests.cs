using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Quartz;

namespace Hodnota.Infrastructure.Tests.Catalog;

public class ScheduledRefreshJobTests
{
    private static readonly Guid SharePageId = Guid.NewGuid();

    private static IStreamingProvider NewProvider(string providerCode)
    {
        var provider = Substitute.For<IStreamingProvider>();
        provider.ProviderCode.Returns(providerCode);
        provider.LinkPlatformCodes.Returns([providerCode]);
        provider.Supports(Arg.Any<StreamingResultType>()).Returns(true);
        provider.SupportsLookup(Arg.Any<StreamingResultType>()).Returns(true);
        provider.LookupAsync(Arg.Any<StreamingLookupKey>(), Arg.Any<CancellationToken>()).Returns([]);
        return provider;
    }

    private static EnrichmentRequest Request() => new(StreamingResultType.Track, "Song", "Artist", "USRC17607839", null, []);

    private static (ScheduledRefreshJob Job, ICatalogRepository Repository) NewJob(IRefreshSettingsCache settingsCache, params IStreamingProvider[] providers) =>
        NewJob(settingsCache, NothingActiveQueue(), providers);

    private static (ScheduledRefreshJob Job, ICatalogRepository Repository) NewJob(
        IRefreshSettingsCache settingsCache, IEnrichmentQueue queue, params IStreamingProvider[] providers)
    {
        var repository = Substitute.For<ICatalogRepository>();
        var services = new ServiceCollection();
        services.AddSingleton(repository);
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<CatalogEnrichmentService>>(NullLogger<CatalogEnrichmentService>.Instance);
        foreach (var provider in providers)
        {
            services.AddSingleton(provider);
        }

        services.AddScoped<CatalogEnrichmentService>();
        var serviceProvider = services.BuildServiceProvider();

        var job = new ScheduledRefreshJob(serviceProvider.GetRequiredService<IServiceScopeFactory>(), settingsCache, queue, NullLogger<ScheduledRefreshJob>.Instance);
        return (job, repository);
    }

    // Matches every test below that doesn't care about the EnrichmentWorker-coordination path.
    private static IEnrichmentQueue NothingActiveQueue()
    {
        var queue = Substitute.For<IEnrichmentQueue>();
        queue.IsActive(Arg.Any<Guid>(), Arg.Any<string>()).Returns(false);
        return queue;
    }

    private static IRefreshSettingsCache Settings(int notFoundDays = 7, int foundDays = 90)
    {
        var cache = Substitute.For<IRefreshSettingsCache>();
        cache.GetIntervalsAsync(Arg.Any<CancellationToken>()).Returns(new RefreshIntervals(notFoundDays, foundDays));
        return cache;
    }

    [Fact]
    public async Task NoDueCandidates_SavesNothing()
    {
        var (job, repository) = NewJob(Settings());
        repository.GetDueProviderChecksAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await job.Execute(Substitute.For<IJobExecutionContext>());

        await repository.DidNotReceive().GetEnrichmentRequestAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DueCandidate_EnrichesOnlyTheDueProviderAndSavesTheResult()
    {
        var provider = NewProvider(ProviderCodes.Tidal);
        var (job, repository) = NewJob(Settings(), provider);
        repository.GetDueProviderChecksAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new DueRefreshCandidate(SharePageId, PlatformCodes.Tidal)]);
        repository.GetEnrichmentRequestAsync(SharePageId, Arg.Any<CancellationToken>()).Returns(Request());

        await job.Execute(Substitute.For<IJobExecutionContext>());

        await repository.Received(1).SaveEnrichmentAsync(SharePageId, Arg.Is<ProviderEnrichment>(e => e.ProviderCode == ProviderCodes.Tidal), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SharePageNoLongerExists_SkipsItWithoutThrowing()
    {
        var provider = NewProvider(ProviderCodes.Tidal);
        var (job, repository) = NewJob(Settings(), provider);
        repository.GetDueProviderChecksAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new DueRefreshCandidate(SharePageId, PlatformCodes.Tidal)]);
        repository.GetEnrichmentRequestAsync(SharePageId, Arg.Any<CancellationToken>()).Returns((EnrichmentRequest?)null);

        await job.Execute(Substitute.For<IJobExecutionContext>());

        await repository.DidNotReceive().SaveEnrichmentAsync(Arg.Any<Guid>(), Arg.Any<ProviderEnrichment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OneCandidateThrows_OtherCandidatesStillGetProcessed()
    {
        var otherPageId = Guid.NewGuid();
        var provider = NewProvider(ProviderCodes.Tidal);
        var (job, repository) = NewJob(Settings(), provider);
        repository.GetDueProviderChecksAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new DueRefreshCandidate(SharePageId, PlatformCodes.Tidal), new DueRefreshCandidate(otherPageId, PlatformCodes.Tidal)]);
        repository.GetEnrichmentRequestAsync(SharePageId, Arg.Any<CancellationToken>()).Throws(new InvalidOperationException("boom"));
        repository.GetEnrichmentRequestAsync(otherPageId, Arg.Any<CancellationToken>()).Returns(Request());

        await job.Execute(Substitute.For<IJobExecutionContext>());

        await repository.Received(1).SaveEnrichmentAsync(otherPageId, Arg.Any<ProviderEnrichment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnlyRegisteredInScopeProviders_AreAskedForDueChecks()
    {
        var (job, repository) = NewJob(Settings(), NewProvider(ProviderCodes.Tidal), NewProvider(ProviderCodes.YouTube));
        repository.GetDueProviderChecksAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await job.Execute(Substitute.For<IJobExecutionContext>());

        await repository.Received(1).GetDueProviderChecksAsync(
            Arg.Is<IReadOnlyList<string>>(codes => codes.SequenceEqual(new[] { PlatformCodes.Tidal })),
            Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoInScopeProviderRegistered_DoesNotQueryForDueChecks()
    {
        var (job, repository) = NewJob(Settings(), NewProvider(ProviderCodes.YouTube));

        await job.Execute(Substitute.For<IJobExecutionContext>());

        await repository.DidNotReceive().GetDueProviderChecksAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PageFailsBeforeAnyProviderAnswers_DueProviderIsRecordedAsFailed()
    {
        var (job, repository) = NewJob(Settings(), NewProvider(ProviderCodes.Tidal));
        repository.GetDueProviderChecksAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new DueRefreshCandidate(SharePageId, PlatformCodes.Tidal)]);
        repository.GetEnrichmentRequestAsync(SharePageId, Arg.Any<CancellationToken>()).Throws(new InvalidOperationException("boom"));

        await job.Execute(Substitute.For<IJobExecutionContext>());

        await repository.Received(1).SaveEnrichmentAsync(
            SharePageId,
            Arg.Is<ProviderEnrichment>(e => e.ProviderCode == ProviderCodes.Tidal && e.Outcome == LookupOutcome.Failed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SavingAResultFails_ProviderIsRecordedAsFailed()
    {
        var (job, repository) = NewJob(Settings(), NewProvider(ProviderCodes.Tidal));
        repository.GetDueProviderChecksAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new DueRefreshCandidate(SharePageId, PlatformCodes.Tidal)]);
        repository.GetEnrichmentRequestAsync(SharePageId, Arg.Any<CancellationToken>()).Returns(Request());
        repository.SaveEnrichmentAsync(SharePageId, Arg.Is<ProviderEnrichment>(e => e.Outcome != LookupOutcome.Failed), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("save failed"));

        await job.Execute(Substitute.For<IJobExecutionContext>());

        await repository.Received(1).SaveEnrichmentAsync(
            SharePageId,
            Arg.Is<ProviderEnrichment>(e => e.ProviderCode == ProviderCodes.Tidal && e.Outcome == LookupOutcome.Failed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProviderAlreadyActiveInTheEnrichmentQueue_IsSkipped()
    {
        var provider = NewProvider(ProviderCodes.Tidal);
        var queue = Substitute.For<IEnrichmentQueue>();
        queue.IsActive(SharePageId, ProviderCodes.Tidal).Returns(true);
        var (job, repository) = NewJob(Settings(), queue, provider);
        repository.GetDueProviderChecksAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new DueRefreshCandidate(SharePageId, PlatformCodes.Tidal)]);

        await job.Execute(Substitute.For<IJobExecutionContext>());

        await repository.DidNotReceive().GetEnrichmentRequestAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await repository.DidNotReceive().SaveEnrichmentAsync(Arg.Any<Guid>(), Arg.Any<ProviderEnrichment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancelled_Throws_AndRecordsNothingAsFailed()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var (job, repository) = NewJob(Settings(), NewProvider(ProviderCodes.Tidal));
        repository.GetDueProviderChecksAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new DueRefreshCandidate(SharePageId, PlatformCodes.Tidal)]);
        repository.GetEnrichmentRequestAsync(SharePageId, Arg.Any<CancellationToken>()).Throws(new OperationCanceledException(cancellation.Token));

        var act = async () => await job.Execute(Substitute.For<IJobExecutionContext>(), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await repository.DidNotReceive().SaveEnrichmentAsync(Arg.Any<Guid>(), Arg.Any<ProviderEnrichment>(), Arg.Any<CancellationToken>());
    }
}
