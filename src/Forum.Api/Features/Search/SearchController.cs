using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Api.Features.Search;

[ApiController]
[AllowAnonymous]
[Route("api/search")]
public class SearchController(SearchService searchService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<SearchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] SearchRequest request, CancellationToken cancellationToken)
    {
        var result = await searchService.SearchAsync(request, cancellationToken);
        return result.Failure == SearchFailure.None
            ? Ok(result.Response)
            : BadRequest(new ProblemDetails { Title = "Provide a non-empty search query and valid pagination." });
    }
}
