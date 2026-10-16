using Hodnota.Application.Catalog;
using Hodnota.Contracts.Admin;
using Hodnota.Infrastructure.Identity;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hodnota.Api.Admin;

[ApiController]
[Route("api/admin/refresh-settings")]
[Authorize(Policy = AdminConfiguration.RoleName)]
public sealed class RefreshSettingsController(ICatalogRepository repository, IRefreshSettingsCache cache) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RefreshSettingsResponse>> Get(CancellationToken cancellationToken)
    {
        var intervals = await cache.GetIntervalsAsync(cancellationToken);
        return Ok(intervals.ToResponse());
    }

    [HttpPut]
    public async Task<ActionResult<RefreshSettingsResponse>> Update(UpdateRefreshSettingsRequest request, CancellationToken cancellationToken)
    {
        var intervals = request.ToIntervals();
        await repository.UpdateRefreshIntervalsAsync(intervals, cancellationToken);
        cache.Invalidate();
        return Ok(intervals.ToResponse());
    }
}
