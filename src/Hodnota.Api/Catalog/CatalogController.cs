using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Hodnota.Application.Catalog;
using Hodnota.Contracts.Catalog;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    public async Task<IResult> WatchSharePage(Guid id, CancellationToken cancellationToken)
    {
        return !await sharePageService.ExistsAsync(id, cancellationToken)
            ? TypedResults.NotFound()
            : TypedResults.ServerSentEvents(ToSseItems(sharePageService.WatchAsync(id, cancellationToken), jsonOptions.Value.JsonSerializerOptions, cancellationToken));
    }

    private static async IAsyncEnumerable<SseItem<string>> ToSseItems(
        IAsyncEnumerable<SharePageEvent> events,
        JsonSerializerOptions jsonSerializerOptions,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var sharePageEvent in events.WithCancellation(cancellationToken))
        {
            yield return sharePageEvent switch
            {
                PlatformRowEvent platform => new SseItem<string>(JsonSerializer.Serialize(platform.Row.ToResponse(), jsonSerializerOptions), "platform"),
                _ => new SseItem<string>("{}", "complete"),
            };
        }
    }
}
