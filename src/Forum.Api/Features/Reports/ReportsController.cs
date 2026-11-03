using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Api.Features.Reports;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController(ReportsService reportsService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreateReportResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateReportRequest request, CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var reporterId))
        {
            return Unauthorized();
        }

        var result = await reportsService.CreateAsync(reporterId, request, cancellationToken);
        return result.Failure switch
        {
            CreateReportFailure.None => StatusCode(StatusCodes.Status201Created, result.Response),
            CreateReportFailure.Banned => Forbid(),
            CreateReportFailure.ReporterNotFound or CreateReportFailure.PostNotFound =>
                NotFound(new ProblemDetails { Title = "Post or reporter was not found." }),
            CreateReportFailure.DuplicateReport => Conflict(new ProblemDetails
            {
                Title = "You have already reported this post."
            }),
            _ => BadRequest(new ProblemDetails { Title = "Provide a post and non-empty reason." })
        };
    }
}
