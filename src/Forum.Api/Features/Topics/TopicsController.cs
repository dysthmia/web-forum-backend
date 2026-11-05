using System.Security.Claims;
using Forum.Api.Common.Authorization;
using Forum.Api.Infrastructure.Redis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Api.Features.Topics;

[ApiController]
[Authorize]
[Route("api/topics")]
public class TopicsController(
    TopicsService topicsService,
    ViewCounterService viewCounterService,
    RateLimitService rateLimitService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType<PagedResponse<TopicListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTopics(
        [FromQuery] GetTopicsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await topicsService.GetTopicsAsync(request, cancellationToken);
        return result.Failure == TopicReadFailure.None
            ? Ok(result.Response)
            : BadRequest(new ProblemDetails { Title = "Provide valid pagination and sort values." });
    }

    [AllowAnonymous]
    [HttpGet("/api/categories/{categoryId:long}/topics")]
    [ProducesResponseType<PagedResponse<TopicListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategoryTopics(
        long categoryId,
        [FromQuery] GetCategoryTopicsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await topicsService.GetCategoryTopicsAsync(categoryId, request, cancellationToken);
        return result.Failure switch
        {
            TopicReadFailure.None => Ok(result.Response),
            TopicReadFailure.CategoryNotFound => NotFound(new ProblemDetails { Title = "Category was not found." }),
            _ => BadRequest(new ProblemDetails { Title = "Provide valid pagination and sort values." })
        };
    }

    [AllowAnonymous]
    [HttpGet("{topicId:long}")]
    [ProducesResponseType<TopicDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetails(
        long topicId,
        [FromQuery] TopicPageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await topicsService.GetDetailsAsync(topicId, request, cancellationToken);
        if (result.Failure == TopicReadFailure.None)
        {
            await viewCounterService.TryRecordViewAsync(topicId, HttpContext, cancellationToken);
        }

        return result.Failure switch
        {
            TopicReadFailure.None => Ok(result.Response),
            TopicReadFailure.TopicNotFound => NotFound(new ProblemDetails { Title = "Topic was not found." }),
            _ => BadRequest(new ProblemDetails { Title = "Provide valid pagination values." })
        };
    }

    [HttpPut("{topicId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTitle(
        long topicId,
        UpdateTopicRequest request,
        CancellationToken cancellationToken) =>
        ToManagementResponse(await topicsService.UpdateTitleAsync(topicId, GetActor(), request, cancellationToken));

    [Authorize(Policy = "Moderator")]
    [HttpPut("{topicId:long}/closed")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetClosed(
        long topicId,
        SetTopicClosedRequest request,
        CancellationToken cancellationToken) =>
        ToManagementResponse(await topicsService.SetClosedAsync(topicId, GetActor(), request, cancellationToken));

    [Authorize(Policy = "Moderator")]
    [HttpPut("{topicId:long}/pinned")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPinned(
        long topicId,
        SetTopicPinnedRequest request,
        CancellationToken cancellationToken) =>
        ToManagementResponse(await topicsService.SetPinnedAsync(topicId, GetActor(), request, cancellationToken));

    [HttpDelete("{topicId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long topicId, CancellationToken cancellationToken) =>
        ToManagementResponse(await topicsService.DeleteAsync(topicId, GetActor(), cancellationToken));

    [HttpPost]
    [ProducesResponseType<CreateTopicResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create(CreateTopicRequest request, CancellationToken cancellationToken)
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

        var result = await topicsService.CreateAsync(authorId, request, cancellationToken);
        return result.Failure switch
        {
            CreateTopicFailure.None => StatusCode(StatusCodes.Status201Created, result.Response),
            CreateTopicFailure.CategoryNotFound or CreateTopicFailure.AuthorNotFound =>
                NotFound(new ProblemDetails { Title = "Category or author was not found." }),
            CreateTopicFailure.Banned => Forbid(),
            CreateTopicFailure.CreationFailed => Conflict(new ProblemDetails
            {
                Title = "Topic could not be created."
            }),
            _ => BadRequest(new ProblemDetails { Title = "Provide a valid category, title, and first message." })
        };
    }

    private TopicActor GetActor()
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);
        return new TopicActor(userId, User.IsInRole(ForumRoles.Moderator) || User.IsInRole(ForumRoles.Admin));
    }

    private IActionResult ToManagementResponse(TopicManagementFailure failure) => failure switch
    {
        TopicManagementFailure.None => NoContent(),
        TopicManagementFailure.Forbidden => Forbid(),
        TopicManagementFailure.NotFound => NotFound(new ProblemDetails { Title = "Topic was not found." }),
        _ => BadRequest(new ProblemDetails { Title = "Provide a valid topic title." })
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
