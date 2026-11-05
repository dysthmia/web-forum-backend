using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Api.Features.Bookmarks;

[ApiController]
[Authorize]
[Route("api/bookmarks")]
public class BookmarksController(BookmarksService bookmarksService) : ControllerBase
{
    [HttpGet("{topicId:long}")]
    [ProducesResponseType<BookmarkStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(long topicId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await bookmarksService.GetStatusAsync(userId, topicId, cancellationToken);
        return result.Failure switch
        {
            BookmarkFailure.None => Ok(result.Response),
            BookmarkFailure.Banned => Forbid(),
            _ => NotFound(new ProblemDetails { Title = "Topic or user was not found." })
        };
    }

    [HttpGet]
    [ProducesResponseType<BookmarkListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetList([FromQuery] BookmarkPageRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await bookmarksService.GetListAsync(userId, request, cancellationToken);
        return result.Failure switch
        {
            BookmarkFailure.None => Ok(result.Response),
            BookmarkFailure.Banned => Forbid(),
            BookmarkFailure.UserNotFound => NotFound(new ProblemDetails { Title = "User was not found." }),
            _ => BadRequest(new ProblemDetails { Title = "Provide valid pagination values." })
        };
    }

    [HttpPut("{topicId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Add(long topicId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return ToActionResult(await bookmarksService.AddAsync(userId, topicId, cancellationToken));
    }

    [HttpDelete("{topicId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Remove(long topicId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return ToActionResult(await bookmarksService.RemoveAsync(userId, topicId, cancellationToken));
    }

    private bool TryGetCurrentUserId(out long userId) =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private IActionResult ToActionResult(BookmarkFailure failure) => failure switch
    {
        BookmarkFailure.None => NoContent(),
        BookmarkFailure.Banned => Forbid(),
        BookmarkFailure.TopicNotFound or BookmarkFailure.UserNotFound =>
            NotFound(new ProblemDetails { Title = "Topic or user was not found." }),
        _ => BadRequest(new ProblemDetails { Title = "Provide a valid bookmark request." })
    };
}
