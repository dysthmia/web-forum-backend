using Forum.Api.Common.Authorization;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Forum.Api.Features.Auth;

public enum AuthFailure
{
    None,
    DuplicateUser,
    InvalidCredentials,
    Banned,
    InvalidRefreshToken
}

public record AuthResult(AuthResponse? Response, AuthFailure Failure)
{
    public static AuthResult Failed(AuthFailure failure) => new(null, failure);

    public static AuthResult Succeeded(AuthResponse response) => new(response, AuthFailure.None);
}

public class AuthService(
    ForumDbContext dbContext,
    PasswordService passwordService,
    RefreshTokenService refreshTokenService,
    ActiveUserBanService banService)
{
    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim();
        var normalizedUsername = username.ToLowerInvariant();
        var normalizedEmail = email.ToLowerInvariant();

        var alreadyExists = await dbContext.Users.AnyAsync(
            user => user.Username.ToLower() == normalizedUsername || user.Email.ToLower() == normalizedEmail,
            cancellationToken);
        if (alreadyExists)
        {
            return AuthResult.Failed(AuthFailure.DuplicateUser);
        }

        var user = new User
        {
            Username = username,
            Email = email,
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordService.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return AuthResult.Failed(AuthFailure.DuplicateUser);
        }

        var tokens = await refreshTokenService.CreateAsync(user, cancellationToken);
        return AuthResult.Succeeded(ToResponse(user, tokens));
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var identity = request.UsernameOrEmail.Trim().ToLowerInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.Username.ToLower() == identity || item.Email.ToLower() == identity,
            cancellationToken);

        if (!passwordService.VerifyPassword(user, request.Password))
        {
            return AuthResult.Failed(AuthFailure.InvalidCredentials);
        }

        if (await banService.IsBannedAsync(user!.Id, cancellationToken))
        {
            return AuthResult.Failed(AuthFailure.Banned);
        }

        var tokens = await refreshTokenService.CreateAsync(user, cancellationToken);
        return AuthResult.Succeeded(ToResponse(user, tokens));
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var token = await refreshTokenService.RotateAsync(refreshToken, cancellationToken);
        return token is null
            ? AuthResult.Failed(AuthFailure.InvalidRefreshToken)
            : AuthResult.Succeeded(token);
    }

    public Task LogoutAsync(string refreshToken, CancellationToken cancellationToken) =>
        refreshTokenService.RevokeAsync(refreshToken, cancellationToken);

    private static AuthResponse ToResponse(User user, TokenPair tokens) =>
        new(user.Id, user.Username, user.Email, user.Role.ToString().ToLowerInvariant(), tokens.AccessToken, tokens.RefreshToken);
}
