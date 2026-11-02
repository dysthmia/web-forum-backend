using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Api.Features.Posts;

[ApiController]
[Authorize]
[Route("api/topics/{topicId:long}/posts")]
public class PostsController(PostsService postsService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreatePostResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        long topicId,
        CreatePostRequest request,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var authorId))
        {
            return Unauthorized();
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
}
