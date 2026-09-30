using System.Text.Json;

using Hodnota.Application.Catalog;
using Hodnota.Contracts.Catalog;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hodnota.Api.Catalog;

[ApiController]
[Route("api/catalog")]
[Authorize]
public sealed class CatalogController(
    CatalogSearchService searchService,
    SharePageService sharePageService,
    IOptions<JsonOptions> jsonOptions) : ControllerBase
{
    [HttpPost("search")]
    public async Task<ActionResult<IReadOnlyList<SearchCandidateResponse>>> Search(SearchRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<CatalogSearchCandidate> candidates;
        try
        {
            candidates = await searchService.SearchAsync(request.Search, request.Type.ToStreamingResultType(), cancellationToken);
        }
        catch (StreamingProviderException)
        {
            return BadRequest("The search providers are currently unavailable.");
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

    // Server-Sent Events: a `platform` event for each row that is settled (already known ones first),
    // then one `complete` event. The data of a `platform` event is a PlatformRowResponse.
    [HttpGet("sharepages/{id:guid}/events")]
    [AllowAnonymous]
    [Produces("text/event-stream")]
    public async Task<IActionResult> WatchSharePage(Guid id, CancellationToken cancellationToken)
    {
        if (await sharePageService.GetAsync(id, cancellationToken) is null)
        {
            return NotFound();
        }

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        await Response.StartAsync(cancellationToken);

        try
        {
            await foreach (var sharePageEvent in sharePageService.WatchAsync(id, cancellationToken))
            {
                var (name, data) = sharePageEvent switch
                {
                    PlatformRowEvent platform => ("platform", JsonSerializer.Serialize(platform.Row.ToResponse(), jsonOptions.Value.JsonSerializerOptions)),
                    _ => ("complete", "{}"),
                };

                await Response.WriteAsync($"event: {name}\ndata: {data}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The viewer closed the page.
        }

        return new EmptyResult();
    }
}
