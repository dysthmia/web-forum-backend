using System.ComponentModel.DataAnnotations;

namespace Forum.Api.Features.Auth;

public class RegisterRequest
{
    [Required, StringLength(32, MinimumLength = 2)]
    [RegularExpression("^[a-zA-Z0-9_]+$")]
    public string Username { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;
}

public class LoginRequest
{
    [Required, StringLength(254, MinimumLength = 3)]
    public string UsernameOrEmail { get; init; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;
}

public class RefreshRequest
{
    [Required, StringLength(1024, MinimumLength = 32)]
    public string RefreshToken { get; init; } = string.Empty;
}

public class LogoutRequest
{
    [Required, StringLength(1024, MinimumLength = 32)]
    public string RefreshToken { get; init; } = string.Empty;
}

public record AuthResponse(long UserId, string Username, string Email, string Role, string AccessToken, string RefreshToken);
