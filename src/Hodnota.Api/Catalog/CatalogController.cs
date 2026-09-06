using Hodnota.Application.Catalog;
using Hodnota.Contracts.Catalog;

using Microsoft.AspNetCore.Mvc;

namespace Hodnota.Api.Catalog;

[ApiController]
[Route("api/catalog")]
public sealed class CatalogController(CatalogSearchService searchService, SharePageCreationService sharePageService) : ControllerBase
{
    [HttpPost("search")]
    public async Task<ActionResult<IReadOnlyList<SearchCandidateResponse>>> Search(SearchRequest request, CancellationToken cancellationToken)
    {
        var candidates = await searchService.SearchAsync(request.Search, cancellationToken);

        IReadOnlyList<SearchCandidateResponse> response = [.. candidates.Select(candidate => candidate.ToResponse())];
        return Ok(response);
    }

    [HttpPost("resolve")]
    public async Task<ActionResult<SharePageResponse>> Resolve(ResolveRequest request, CancellationToken cancellationToken)
    {
        var result = await sharePageService.ResolveAsync(request.Id, cancellationToken);

        return result is null ? NotFound() : Ok(result.ToResponse());
    }
}
