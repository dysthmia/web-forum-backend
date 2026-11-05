using System.Security.Claims;
using Forum.Api.Common.Authorization;
using Forum.Api.Infrastructure.Redis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Api.Features.Posts;

[ApiController]
[Authorize]
[Route("api/topics/{topicId:long}/posts")]
public class PostsController(PostsService postsService, RateLimitService rateLimitService) : ControllerBase
{
    [HttpPut("{postId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        long topicId,
        long postId,
        UpdatePostRequest request,
        CancellationToken cancellationToken) =>
        ToManagementResponse(await postsService.UpdateAsync(topicId, postId, GetActor(), request, cancellationToken));

    [HttpDelete("{postId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long topicId, long postId, CancellationToken cancellationToken) =>
        ToManagementResponse(await postsService.DeleteAsync(topicId, postId, GetActor(), cancellationToken));

    [HttpPost]
    [ProducesResponseType<CreatePostResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create(
        long topicId,
        CreatePostRequest request,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var authorId))
        {
            return Unauthorized();
        }

        var rateLimitResponse = await GetRateLimitResponseAsync(authorId, cancellationToken);
        if (rateLimitResponse is not null)
        {
            return rateLimitResponse;
        }

        var result = await postsService.CreateAsync(topicId, authorId, request, cancellationToken);
        return result.Failure switch
        {
            CreatePostFailure.None => StatusCode(StatusCodes.Status201Created, result.Response),
            CreatePostFailure.TopicNotFound or CreatePostFailure.AuthorNotFound =>
                NotFound(new ProblemDetails { Title = "Topic or author was not found." }),
            CreatePostFailure.Banned => Forbid(),
            CreatePostFailure.TopicClosed => Conflict(new ProblemDetails { Title = "Topic is closed." }),
            CreatePostFailure.CreationFailed => Conflict(new ProblemDetails { Title = "Post could not be created." }),
            _ => BadRequest(new ProblemDetails { Title = "Provide non-empty post content." })
        };
    }

    private PostActor GetActor()
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);
        return new PostActor(userId, User.IsInRole(ForumRoles.Moderator) || User.IsInRole(ForumRoles.Admin));
    }

    private IActionResult ToManagementResponse(PostManagementFailure failure) => failure switch
    {
        PostManagementFailure.None => NoContent(),
        PostManagementFailure.Forbidden => Forbid(),
        PostManagementFailure.NotFound => NotFound(new ProblemDetails { Title = "Post was not found." }),
        PostManagementFailure.FirstPostCannotBeDeleted => Conflict(new ProblemDetails
        {
            Title = "The first post can only be removed by deleting its topic."
        }),
        PostManagementFailure.Deleted => Conflict(new ProblemDetails { Title = "Deleted posts cannot be changed." }),
        _ => BadRequest(new ProblemDetails { Title = "Provide non-empty post content." })
    };

    private async Task<IActionResult?> GetRateLimitResponseAsync(long userId, CancellationToken cancellationToken)
    {
        var rateLimit = await rateLimitService.CheckContentCreationAsync(userId, cancellationToken);
        if (rateLimit.Status == RateLimitStatus.Unavailable)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Content publishing is temporarily unavailable."
            });
        }

        if (rateLimit.Status != RateLimitStatus.Limited)
        {
            return null;
        }

        Response.Headers.RetryAfter = Math.Ceiling(rateLimit.RetryAfter.TotalSeconds).ToString();
        return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
        {
            Title = "Too many content submissions. Try again later."
        });
    }
}
