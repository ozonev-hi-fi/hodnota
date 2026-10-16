using Hodnota.Application.Catalog;
using Hodnota.Contracts.Admin;

namespace Hodnota.Api.Admin;

public static class RefreshSettingsMappingExtensions
{
    public static RefreshSettingsResponse ToResponse(this RefreshIntervals intervals) =>
        new(intervals.NotFoundRecheckIntervalDays, intervals.FoundRecheckIntervalDays);

    public static RefreshIntervals ToIntervals(this UpdateRefreshSettingsRequest request) =>
        new(request.NotFoundRecheckIntervalDays, request.FoundRecheckIntervalDays);
}
