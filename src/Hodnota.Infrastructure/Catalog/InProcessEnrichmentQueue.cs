using System.Threading.Channels;

using Hodnota.Application.Catalog;

namespace Hodnota.Infrastructure.Catalog;

public sealed record EnrichmentJob(Guid SharePageId, IReadOnlyList<string> ProviderCodes);

// Jobs live in memory: a restart drops the queued ones. Their share pages have no saved check yet,
// so they are queued again the next time someone resolves or watches that page.
public sealed class InProcessEnrichmentQueue : IEnrichmentQueue
{
    private readonly Channel<EnrichmentJob> _channel = Channel.CreateUnbounded<EnrichmentJob>();
    private readonly HashSet<(Guid SharePageId, string ProviderCode)> _active = [];
    private readonly Lock _gate = new();

    public ChannelReader<EnrichmentJob> Reader => _channel.Reader;

    public void Enqueue(Guid sharePageId, IReadOnlyCollection<string> providerCodes)
    {
        List<string> fresh;
        lock (_gate)
        {
            fresh = [.. providerCodes.Where(code => _active.Add((sharePageId, code)))];
        }

        if (fresh.Count > 0)
        {
            _channel.Writer.TryWrite(new EnrichmentJob(sharePageId, fresh));
        }
    }

    public bool IsActive(Guid sharePageId, string providerCode)
    {
        lock (_gate)
        {
            return _active.Contains((sharePageId, providerCode));
        }
    }

    public void Release(Guid sharePageId, string providerCode)
    {
        lock (_gate)
        {
            _active.Remove((sharePageId, providerCode));
        }
    }
}
