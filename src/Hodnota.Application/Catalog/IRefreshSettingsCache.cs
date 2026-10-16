namespace Hodnota.Application.Catalog;

public sealed record RefreshIntervals(int NotFoundRecheckIntervalDays, int FoundRecheckIntervalDays);

public interface IRefreshSettingsCache
{
    Task<RefreshIntervals> GetIntervalsAsync(CancellationToken cancellationToken);

    void Invalidate();
}
