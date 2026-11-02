using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Api.Features.Topics;

[ApiController]
[Authorize]
[Route("api/topics")]
public class TopicsController(TopicsService topicsService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreateTopicResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateTopicRequest request, CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var authorId))
        {
            return Unauthorized();
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
}
