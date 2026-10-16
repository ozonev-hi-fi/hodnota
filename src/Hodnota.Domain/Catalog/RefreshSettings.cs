using System.ComponentModel;

namespace Hodnota.Domain.Catalog;

// Single-row table — app logic (RefreshSettingsCache) always reads/updates the one seeded row.
public sealed class RefreshSettings : IHasTimestamps
{
    public Guid Id { get; set; }

    [Description("How many days a 'not found' platform check is trusted before the scheduled refresh re-checks it.")]
    public int NotFoundRecheckIntervalDays { get; set; }

    [Description("How many days a confirmed platform link is trusted before the scheduled refresh re-verifies it.")]
    public int FoundRecheckIntervalDays { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
