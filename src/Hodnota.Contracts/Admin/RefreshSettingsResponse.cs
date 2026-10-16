namespace Hodnota.Contracts.Admin;

public sealed record RefreshSettingsResponse(int NotFoundRecheckIntervalDays, int FoundRecheckIntervalDays);
