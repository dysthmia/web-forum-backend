using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Forum.Api.Infrastructure.Redis;

namespace Forum.Api.Features.Auth;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public class AuthController(
    AuthService authService,
    RateLimitService rateLimitService,
    RefreshTokenOptions refreshTokenOptions,
    RefreshSessionOptions refreshSessionOptions) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        if (result.Failure == AuthFailure.DuplicateUser)
        {
            return Conflict(new ProblemDetails { Title = "Username or email is already in use." });
        }

        SetRefreshCookie(result.RefreshToken!);
        return Created("", result.Response);
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
        if (result.Failure != AuthFailure.None)
        {
            return Unauthorized(new ProblemDetails { Title = "Invalid credentials or account is unavailable." });
        }

        SetRefreshCookie(result.RefreshToken!);
        return Ok(result.Response);
    }

    /// <summary>Rotates a refresh token.</summary>
    /// <remarks>
    /// Production clients send the HttpOnly refresh cookie. The optional body is only for local Swagger testing.
    /// Requests with an Origin header must match CORS_ALLOWED_ORIGINS.
    /// </remarks>
    [HttpPost("refresh")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        if (!HasAllowedOrigin())
        {
            return Forbid();
        }

        var refreshToken = GetRefreshToken(request.RefreshToken);
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new ProblemDetails { Title = "Refresh token is invalid or account is unavailable." });
        }

        var result = await authService.RefreshAsync(refreshToken, cancellationToken);
        if (result.Failure != AuthFailure.None)
        {
            DeleteRefreshCookie();
            return Unauthorized(new ProblemDetails { Title = "Refresh token is invalid or account is unavailable." });
        }

        SetRefreshCookie(result.RefreshToken!);
        return Ok(result.Response);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        if (!HasAllowedOrigin())
        {
            return Forbid();
        }

        var refreshToken = GetRefreshToken(request.RefreshToken);
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await authService.LogoutAsync(refreshToken, cancellationToken);
        }

        DeleteRefreshCookie();
        return NoContent();
    }

    private string? GetRefreshToken(string? bodyToken) =>
        Request.Cookies[refreshSessionOptions.CookieName] ?? bodyToken;

    private bool HasAllowedOrigin()
    {
        var origin = Request.Headers.Origin.ToString();
        return string.IsNullOrEmpty(origin)
               || refreshSessionOptions.AllowedOrigins.Contains(origin.TrimEnd('/'), StringComparer.Ordinal);
    }

    private void SetRefreshCookie(string refreshToken) =>
        Response.Cookies.Append(refreshSessionOptions.CookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = refreshSessionOptions.CookieSecure,
            SameSite = refreshSessionOptions.CookieSameSite,
            Path = "/api/auth",
            Expires = DateTimeOffset.UtcNow.AddDays(refreshTokenOptions.LifetimeDays)
        });

    private void DeleteRefreshCookie() =>
        Response.Cookies.Delete(refreshSessionOptions.CookieName, new CookieOptions { Path = "/api/auth" });
}
