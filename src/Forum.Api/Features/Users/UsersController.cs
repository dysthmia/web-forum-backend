using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Api.Features.Users;

[ApiController]
[Route("api/users")]
public class UsersController(UsersService usersService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("by-username/{username}")]
    [ProducesResponseType<PublicUserProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicProfileByUsername(string username, CancellationToken cancellationToken)
    {
        var result = await usersService.GetPublicProfileByUsernameAsync(username, cancellationToken);
        return result.Failure == UserProfileFailure.None
            ? Ok(result.PublicProfile)
            : NotFound(new ProblemDetails { Title = "User was not found." });
    }

    [AllowAnonymous]
    [HttpGet("{userId:long}")]
    [ProducesResponseType<PublicUserProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicProfile(long userId, CancellationToken cancellationToken)
    {
        var result = await usersService.GetPublicProfileAsync(userId, cancellationToken);
        return result.Failure == UserProfileFailure.None
            ? Ok(result.PublicProfile)
            : NotFound(new ProblemDetails { Title = "User was not found." });
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<OwnUserProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOwnProfile(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await usersService.GetOwnProfileAsync(userId, cancellationToken);
        return result.Failure == UserProfileFailure.None
            ? Ok(result.OwnProfile)
            : NotFound(new ProblemDetails { Title = "User was not found." });
    }

    [Authorize]
    [HttpPut("me")]
    [ProducesResponseType<OwnUserProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateOwnProfile(
        UpdateOwnProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await usersService.UpdateOwnProfileAsync(userId, request, cancellationToken);
        return result.Failure switch
        {
            UserProfileFailure.None => Ok(result.OwnProfile),
            UserProfileFailure.DuplicateUser => Conflict(new ProblemDetails
            {
                Title = "Username or email is already in use."
            }),
            UserProfileFailure.InvalidRequest => BadRequest(new ProblemDetails
            {
                Title = "Provide a non-empty username or email."
            }),
            _ => NotFound(new ProblemDetails { Title = "User was not found." })
        };
    }

    private bool TryGetCurrentUserId(out long userId) =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
