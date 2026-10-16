using System.ComponentModel.DataAnnotations;

namespace Hodnota.Contracts.Admin;

public sealed record UpdateRefreshSettingsRequest(
    [Range(1, UpdateRefreshSettingsRequest.MaxIntervalDays)] int NotFoundRecheckIntervalDays,
    [Range(1, UpdateRefreshSettingsRequest.MaxIntervalDays)] int FoundRecheckIntervalDays)
{
    public const int MaxIntervalDays = 3650;
}
