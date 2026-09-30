using System.Runtime.CompilerServices;

using Hodnota.Domain.Catalog;

namespace Hodnota.Application.Catalog;

public sealed class SharePageService(
    ISearchCandidateCache cache,
    ICatalogRepository repository,
    IEnumerable<IStreamingProvider> providers,
    IEnrichmentQueue queue,
    IShareEventHub events)
{
    // A watched page whose checks never finish (a crash mid-job) must not hold the stream open forever.
    private static readonly TimeSpan WatchTimeout = TimeSpan.FromSeconds(60);

    public async Task<SharePageView?> ResolveAsync(string candidateId, CancellationToken cancellationToken)
    {
        var candidate = cache.Get(candidateId);
        if (candidate is null)
        {
            return null;
        }

        var page = await repository.ResolveSharePageAsync(candidate, cancellationToken);
        var checks = await repository.GetPlatformChecksAsync(page.Id, cancellationToken);

        var pending = ProvidersToCheck(page.Type, checks, includeFailed: true);
        if (pending.Count > 0)
        {
            queue.Enqueue(page.Id, pending);
        }

        return await BuildViewAsync(page, checks, cancellationToken);
    }

    public async Task<SharePageView?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var page = await repository.GetSharePageAsync(id, cancellationToken);
        if (page is null)
        {
            return null;
        }

        var checks = await repository.GetPlatformChecksAsync(id, cancellationToken);
        return await BuildViewAsync(page, checks, cancellationToken);
    }

    // Replays what is already known, then follows the running checks until every row is settled.
    // Platforms that were never checked are queued here too, so a page that a restart interrupted
    // (or one saved before enrichment existed) still fills in while someone is looking at it.
    public async IAsyncEnumerable<SharePageEvent> WatchAsync(Guid id, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Subscribed before the snapshot is read, so a result saved in between is not lost.
        using var subscription = events.Subscribe(id);

        var page = await repository.GetSharePageAsync(id, cancellationToken);
        if (page is null)
        {
            yield break;
        }

        var checks = await repository.GetPlatformChecksAsync(id, cancellationToken);
        var pending = ProvidersToCheck(page.Type, checks, includeFailed: false);
        if (pending.Count > 0)
        {
            queue.Enqueue(id, pending);
        }

        var view = await BuildViewAsync(page, checks, cancellationToken);

        // A result can reach the snapshot and the live updates both; a row is sent once per change.
        var sent = new Dictionary<string, PlatformRow>();
        foreach (var row in view.Rows.Where(row => row.State != PlatformRowState.Checking))
        {
            sent[row.PlatformCode] = row;
            yield return new PlatformRowEvent(row);
        }

        if (view.IsComplete)
        {
            yield return new CompleteEvent();
            yield break;
        }

        var waiting = view.Rows.Where(row => row.State == PlatformRowState.Checking).Select(row => row.PlatformCode).ToHashSet();
        var typesByCode = view.Rows.ToDictionary(row => row.PlatformCode, row => row.PlatformType);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(WatchTimeout);
        await foreach (var update in subscription.ReadAllAsync(timeout.Token))
        {
            foreach (var result in update.Where(result => typesByCode.ContainsKey(result.PlatformCode)))
            {
                var row = ToRow(result, typesByCode[result.PlatformCode], result.Url);
                if (!sent.TryGetValue(row.PlatformCode, out var previous) || previous != row)
                {
                    sent[row.PlatformCode] = row;
                    yield return new PlatformRowEvent(row);
                }

                waiting.Remove(result.PlatformCode);
            }

            if (waiting.Count == 0)
            {
                yield return new CompleteEvent();
                yield break;
            }
        }
    }

    private List<string> ProvidersToCheck(StreamingResultType type, IReadOnlyList<PlatformCheckResult> checks, bool includeFailed) =>
    [
        .. providers
            .Where(provider => provider.Supports(type))
            .Where(provider => provider.LinkPlatformCodes.Any(code =>
            {
                var check = checks.FirstOrDefault(c => c.PlatformCode == code);
                return check is null || (includeFailed && check.Outcome == LookupOutcome.Failed);
            }))
            .Select(provider => provider.ProviderCode),
    ];

    private async Task<SharePageView> BuildViewAsync(SharePageResult page, IReadOnlyList<PlatformCheckResult> checks, CancellationToken cancellationToken)
    {
        var expected = ProviderTrustOrder.Sort(providers.Where(provider => provider.Supports(page.Type)))
            .SelectMany(provider => provider.LinkPlatformCodes)
            .Distinct()
            .ToList();
        var types = await repository.GetPlatformTypesAsync(expected, cancellationToken);

        // A platform whose provider is queued or running is being checked again, even if an earlier
        // check (a failed one) is saved: its row must show as checking so the page follows the result.
        var beingChecked = providers
            .Where(provider => queue.IsActive(page.Id, provider.ProviderCode))
            .SelectMany(provider => provider.LinkPlatformCodes)
            .ToHashSet();

        var rows = new List<PlatformRow>();
        foreach (var code in expected.Where(types.ContainsKey))
        {
            var check = checks.FirstOrDefault(c => c.PlatformCode == code);
            var linkUrl = page.Links.FirstOrDefault(link => link.PlatformCode == code)?.Url;
            rows.Add(check is null || beingChecked.Contains(code)
                ? new PlatformRow(code, types[code], PlatformRowState.Checking, null)
                : ToRow(check, types[code], linkUrl));
        }

        // A link whose provider is not running now (for example a dormant Spotify) stays on the page.
        rows.AddRange(page.Links
            .Where(link => !expected.Contains(link.PlatformCode))
            .Select(link => new PlatformRow(link.PlatformCode, link.PlatformType, PlatformRowState.Found, link.Url)));

        return new SharePageView(page, rows, rows.All(row => row.State != PlatformRowState.Checking));
    }

    // A found result whose link is not visible on the page (the owner hid it) shows as not found.
    private static PlatformRow ToRow(PlatformCheckResult check, PlatformType type, Uri? linkUrl)
    {
        var state = check.Outcome switch
        {
            LookupOutcome.ExactMatch when linkUrl is not null => PlatformRowState.Found,
            LookupOutcome.NameMatch when linkUrl is not null => PlatformRowState.OtherVersion,
            LookupOutcome.Failed => PlatformRowState.Failed,
            _ => PlatformRowState.NotFound,
        };

        return new PlatformRow(check.PlatformCode, type, state, state == PlatformRowState.NotFound ? null : linkUrl);
    }
}
