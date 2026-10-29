using System.Security.Cryptography;
using System.Text;
using Forum.Api.Common.Authorization;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Forum.Api.Features.Auth;

public class RefreshTokenOptions
{
    public required int LifetimeDays { get; init; }
}

public record TokenPair(string AccessToken, string RefreshToken);

public class RefreshTokenService(
    ForumDbContext dbContext,
    JwtTokenService jwtTokenService,
    RefreshTokenOptions options,
    ActiveUserBanService banService)
{
    public async Task<TokenPair> CreateAsync(User user, CancellationToken cancellationToken)
    {
        var refreshToken = CreateToken();
        var now = DateTime.UtcNow;

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            CreatedAt = now,
            ExpiresAt = now.AddDays(options.LifetimeDays)
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new TokenPair(jwtTokenService.CreateAccessToken(user), refreshToken);
    }

    public async Task<AuthResponse?> RotateAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var tokenHash = HashToken(refreshToken);
        var token = await dbContext.RefreshTokens
            .AsNoTracking()
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);

        if (token is null || token.ExpiresAt <= now || token.RevokedAt is not null)
        {
            return null;
        }

        if (await banService.IsBannedAsync(token.UserId, cancellationToken))
        {
            return null;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var revoked = await dbContext.RefreshTokens
            .Where(item => item.Id == token.Id && item.RevokedAt == null && item.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokedAt, now), cancellationToken);

        if (revoked != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var newRefreshToken = CreateToken();
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = token.UserId,
            TokenHash = HashToken(newRefreshToken),
            CreatedAt = now,
            ExpiresAt = now.AddDays(options.LifetimeDays)
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AuthResponse(
            token.User.Id,
            token.User.Username,
            token.User.Email,
            token.User.Role.ToString().ToLowerInvariant(),
            jwtTokenService.CreateAccessToken(token.User),
            newRefreshToken);
    }

    public async Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var affected = await dbContext.RefreshTokens
            .Where(item => item.TokenHash == HashToken(refreshToken) && item.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokedAt, now), cancellationToken);
        return affected == 1;
    }

    private static string CreateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string HashToken(string refreshToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}

public static class RefreshTokenOptionsFactory
{
    public static RefreshTokenOptions Create(IConfiguration configuration)
    {
        var lifetimeDays = configuration.GetValue<int?>("JWT_REFRESH_TOKEN_DAYS")
            ?? throw new InvalidOperationException("JWT_REFRESH_TOKEN_DAYS is required.");
        if (lifetimeDays is < 1 or > 365)
        {
            throw new InvalidOperationException("JWT_REFRESH_TOKEN_DAYS must be between 1 and 365.");
        }

        return new RefreshTokenOptions { LifetimeDays = lifetimeDays };
    }
}
