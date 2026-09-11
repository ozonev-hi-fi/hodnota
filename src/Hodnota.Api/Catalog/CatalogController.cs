using Hodnota.Application.Catalog;
using Hodnota.Contracts.Catalog;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hodnota.Api.Catalog;

[ApiController]
[Route("api/catalog")]
[Authorize]
public sealed class CatalogController(
    CatalogSearchService searchService,
    SharePageService sharePageService) : ControllerBase
{
    [HttpPost("search")]
    public async Task<ActionResult<IReadOnlyList<SearchCandidateResponse>>> Search(SearchRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<CatalogSearchCandidate> candidates;
        try
        {
            candidates = await searchService.SearchAsync(request.Search, cancellationToken);
        }
        catch (StreamingProviderException)
        {
            return BadRequest("The search provider is currently unavailable.");
        }

        IReadOnlyList<SearchCandidateResponse> response = [.. candidates.Select(candidate => candidate.ToResponse())];
        return Ok(response);
    }

    [HttpPost("resolve")]
    public async Task<ActionResult<SharePageResponse>> Resolve(ResolveRequest request, CancellationToken cancellationToken)
    {
        var result = await sharePageService.ResolveAsync(request.Id, cancellationToken);

        return result is null ? NotFound() : Ok(result.ToResponse());
    }

    [HttpGet("sharepages/{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<SharePageResponse>> GetSharePage(Guid id, CancellationToken cancellationToken)
    {
        var result = await sharePageService.GetAsync(id, cancellationToken);

        return result is null ? NotFound() : Ok(result.ToResponse());
    }
}
