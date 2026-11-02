using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Api.Features.Reactions;

[ApiController]
[Route("api/posts/{postId:long}/reactions")]
public class ReactionsController(ReactionsService reactionsService) : ControllerBase
{
    [Authorize]
    [HttpPut]
    [ProducesResponseType<ReactionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Set(long postId, SetReactionRequest request, CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var result = await reactionsService.SetAsync(postId, userId, request, cancellationToken);
        return result.Failure switch
        {
            ReactionFailure.None => Ok(result.Response),
            ReactionFailure.PostNotFound or ReactionFailure.AuthorNotFound =>
                NotFound(new ProblemDetails { Title = "Post or user was not found." }),
            ReactionFailure.Banned => Forbid(),
            _ => BadRequest(new ProblemDetails { Title = "Reaction type must be 1 or -1." })
        };
    }

    [Authorize]
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(long postId, CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var failure = await reactionsService.RemoveAsync(postId, userId, cancellationToken);
        return failure switch
        {
            ReactionFailure.None => NoContent(),
            ReactionFailure.Banned => Forbid(),
            _ => NotFound(new ProblemDetails { Title = "Post or user was not found." })
        };
    }

    [AllowAnonymous]
    [HttpGet("/api/reactions/counts")]
    [ProducesResponseType<IReadOnlyList<ReactionCountsResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCounts([FromQuery] long[] postIds, CancellationToken cancellationToken) =>
        Ok(await reactionsService.GetCountsAsync(postIds, cancellationToken));
}
