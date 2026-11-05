using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Api.Features.Users;

[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/admin/users")]
public class AdminUsersController(AdminUsersService adminUsersService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AdminUserListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetList([FromQuery] AdminUserListRequest request, CancellationToken cancellationToken)
    {
        var (response, failure) = await adminUsersService.GetListAsync(request, cancellationToken);
        return failure == AdminUserFailure.None
            ? Ok(response)
            : BadRequest(new ProblemDetails { Title = "Provide valid role and pagination values." });
    }

    [HttpPut("{userId:long}/role")]
    [ProducesResponseType<AdminUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeRole(long userId, ChangeUserRoleRequest request, CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
        {
            return Unauthorized();
        }

        var result = await adminUsersService.ChangeRoleAsync(actorId, userId, request, cancellationToken);
        return result.Failure switch
        {
            AdminUserFailure.None => Ok(result.Response),
            AdminUserFailure.NotFound => NotFound(new ProblemDetails { Title = "User was not found." }),
            AdminUserFailure.Forbidden => Forbid(),
            _ => BadRequest(new ProblemDetails { Title = "Provide a known role." })
        };
    }
}
