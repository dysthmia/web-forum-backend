using System.Security.Claims;
using Forum.Api.Common.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Api.Features.Moderation;

[ApiController]
[Authorize(Policy = "Moderator")]
[Route("api/moderation/reports")]
public class ModerationController(ModerationService moderationService) : ControllerBase
{
    [HttpGet("/api/moderation/bans")]
    [ProducesResponseType<BanListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetBans([FromQuery] BanListRequest request, CancellationToken cancellationToken)
    {
        var (response, failure) = await moderationService.GetBansAsync(request, cancellationToken);
        return failure == BanFailure.None
            ? Ok(response)
            : BadRequest(new ProblemDetails { Title = "Provide valid status and pagination values." });
    }

    [HttpGet("/api/moderation/users/{userId:long}/bans")]
    [ProducesResponseType<IReadOnlyList<BanHistoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBanHistory(long userId, CancellationToken cancellationToken)
    {
        var (history, failure) = await moderationService.GetBanHistoryAsync(userId, GetBanActor(), cancellationToken);
        return failure switch
        {
            BanFailure.None => Ok(history),
            BanFailure.Forbidden => Forbid(),
            _ => NotFound(new ProblemDetails { Title = "User was not found." })
        };
    }

    /// <summary>Creates a ban. Moderators must provide a future expiresAt; admins may use null for a permanent ban.</summary>
    [HttpPost("/api/moderation/bans")]
    [ProducesResponseType<BanResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateBan(CreateBanRequest request, CancellationToken cancellationToken)
    {
        var result = await moderationService.CreateBanAsync(GetBanActor(), request, cancellationToken);
        return result.Failure switch
        {
            BanFailure.None => StatusCode(StatusCodes.Status201Created, result.Response),
            BanFailure.Forbidden => Forbid(),
            BanFailure.UserNotFound => NotFound(new ProblemDetails { Title = "User was not found." }),
            BanFailure.ActiveBanExists => Conflict(new ProblemDetails { Title = "User already has an active ban." }),
            _ => BadRequest(new ProblemDetails { Title = "Provide a valid temporary or permanent ban request." })
        };
    }

    [HttpDelete("/api/moderation/bans/{banId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RevokeBan(long banId, CancellationToken cancellationToken)
    {
        var failure = await moderationService.RevokeBanAsync(banId, GetBanActor(), cancellationToken);
        return failure switch
        {
            BanFailure.None => NoContent(),
            BanFailure.Forbidden => Forbid(),
            BanFailure.BanNotFound or BanFailure.UserNotFound => NotFound(new ProblemDetails { Title = "Ban was not found." }),
            _ => Conflict(new ProblemDetails { Title = "Ban is no longer active." })
        };
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PendingReportResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPendingReports(CancellationToken cancellationToken) =>
        Ok(await moderationService.GetPendingReportsAsync(cancellationToken));

    [HttpPut("{reportId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Review(
        long reportId,
        ReviewReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var reviewerId))
        {
            return Unauthorized();
        }

        var failure = await moderationService.ReviewAsync(reportId, reviewerId, request, cancellationToken);
        return failure switch
        {
            ReviewReportFailure.None => NoContent(),
            ReviewReportFailure.ReportNotFound or ReviewReportFailure.ReviewerNotFound =>
                NotFound(new ProblemDetails { Title = "Report or reviewer was not found." }),
            ReviewReportFailure.AlreadyResolved => Conflict(new ProblemDetails
            {
                Title = "Report has already been resolved with a different decision."
            }),
            ReviewReportFailure.FirstPostCannotBeHidden => Conflict(new ProblemDetails
            {
                Title = "The first post can only be hidden by deleting its topic."
            }),
            _ => BadRequest(new ProblemDetails { Title = "Use accepted or rejected status; only accepted reports can hide a post." })
        };
    }

    private BanActor GetBanActor()
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);
        return new BanActor(userId, User.IsInRole(ForumRoles.Admin));
    }
}
