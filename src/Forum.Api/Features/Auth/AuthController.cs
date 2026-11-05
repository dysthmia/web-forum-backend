using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Forum.Api.Infrastructure.Redis;

namespace Forum.Api.Features.Auth;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public class AuthController(AuthService authService, RateLimitService rateLimitService) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        return result.Failure == AuthFailure.DuplicateUser
            ? Conflict(new ProblemDetails { Title = "Username or email is already in use." })
            : Created("", result.Response);
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var rateLimit = await rateLimitService.CheckLoginAsync(request.UsernameOrEmail, cancellationToken);
        if (rateLimit.Status == RateLimitStatus.Unavailable)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Login is temporarily unavailable."
            });
        }

        if (rateLimit.Status == RateLimitStatus.Limited)
        {
            Response.Headers.RetryAfter = Math.Ceiling(rateLimit.RetryAfter.TotalSeconds).ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Title = "Too many login attempts. Try again later."
            });
        }

        var result = await authService.LoginAsync(request, cancellationToken);
        return result.Failure == AuthFailure.None
            ? Ok(result.Response)
            : Unauthorized(new ProblemDetails { Title = "Invalid credentials or account is unavailable." });
    }

    /// <summary>Rotates a refresh token.</summary>
    /// <remarks>
    /// Refresh tokens are accepted in the body for local Swagger testing. Production clients should keep
    /// them in an HttpOnly, Secure cookie instead of browser storage.
    /// </remarks>
    [HttpPost("refresh")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(request.RefreshToken, cancellationToken);
        return result.Failure == AuthFailure.None
            ? Ok(result.Response)
            : Unauthorized(new ProblemDetails { Title = "Refresh token is invalid or account is unavailable." });
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }
}
