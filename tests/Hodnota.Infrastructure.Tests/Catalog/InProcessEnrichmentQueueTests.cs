using AwesomeAssertions;

using Hodnota.Infrastructure.Catalog;

namespace Hodnota.Infrastructure.Tests.Catalog;

public class InProcessEnrichmentQueueTests
{
    private static readonly Guid PageId = Guid.NewGuid();

    private static List<EnrichmentJob> Drain(InProcessEnrichmentQueue queue)
    {
        var jobs = new List<EnrichmentJob>();
        while (queue.Reader.TryRead(out var job))
        {
            jobs.Add(job);
        }

        return jobs;
    }

    [Fact]
    public void Enqueue_QueuesOneJobWithTheGivenProviders()
    {
        var queue = new InProcessEnrichmentQueue();

        queue.Enqueue(PageId, ["tidal", "qobuz"]);

        var job = Drain(queue).Should().ContainSingle().Subject;
        job.SharePageId.Should().Be(PageId);
        job.ProviderCodes.Should().BeEquivalentTo(["tidal", "qobuz"]);
    }

    [Fact]
    public void Enqueue_ProviderAlreadyQueuedForThePage_IsNotQueuedTwice()
    {
        var queue = new InProcessEnrichmentQueue();
        queue.Enqueue(PageId, ["tidal"]);

        queue.Enqueue(PageId, ["tidal", "qobuz"]);

        Drain(queue).SelectMany(job => job.ProviderCodes).Should().Equal("tidal", "qobuz");
    }

    [Fact]
    public void Enqueue_OnlyProvidersAlreadyActive_QueuesNothing()
    {
        var queue = new InProcessEnrichmentQueue();
        queue.Enqueue(PageId, ["tidal"]);
        Drain(queue);

        queue.Enqueue(PageId, ["tidal"]);

        Drain(queue).Should().BeEmpty();
    }

    [Fact]
    public void Enqueue_SameProviderForAnotherPage_IsQueued()
    {
        var queue = new InProcessEnrichmentQueue();
        queue.Enqueue(PageId, ["tidal"]);

        queue.Enqueue(Guid.NewGuid(), ["tidal"]);

        Drain(queue).Should().HaveCount(2);
    }

    [Fact]
    public void IsActive_IsTrueFromEnqueueUntilRelease()
    {
        var queue = new InProcessEnrichmentQueue();
        queue.IsActive(PageId, "tidal").Should().BeFalse();

        queue.Enqueue(PageId, ["tidal"]);
        queue.IsActive(PageId, "tidal").Should().BeTrue();
        queue.IsActive(PageId, "qobuz").Should().BeFalse();

        queue.Release(PageId, "tidal");
        queue.IsActive(PageId, "tidal").Should().BeFalse();
    }

    [Fact]
    public void Enqueue_AfterRelease_QueuesTheProviderAgain()
    {
        var queue = new InProcessEnrichmentQueue();
        queue.Enqueue(PageId, ["tidal"]);
        Drain(queue);
        queue.Release(PageId, "tidal");

        queue.Enqueue(PageId, ["tidal"]);

        Drain(queue).Should().ContainSingle();
    }
}
