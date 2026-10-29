using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Forum.Api.Features.Auth;

public class JwtOptions
{
    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    public required string Secret { get; init; }

    public required int AccessTokenMinutes { get; init; }
}

public class JwtTokenService(JwtOptions options)
{
    public string CreateAccessToken(User user)
    {
        var now = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString().ToLowerInvariant())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            claims,
            now,
            now.AddMinutes(options.AccessTokenMinutes),
            credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public static class JwtOptionsFactory
{
    public static JwtOptions Create(IConfiguration configuration)
    {
        var issuer = Require(configuration, "JWT_ISSUER");
        var audience = Require(configuration, "JWT_AUDIENCE");
        var secret = Require(configuration, "JWT_SECRET");
        var minutes = configuration.GetValue<int?>("JWT_ACCESS_TOKEN_MINUTES")
            ?? throw new InvalidOperationException("JWT_ACCESS_TOKEN_MINUTES is required.");

        if (Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException("JWT_SECRET must contain at least 32 bytes.");
        }

        if (minutes is < 1 or > 120)
        {
            throw new InvalidOperationException("JWT_ACCESS_TOKEN_MINUTES must be between 1 and 120.");
        }

        return new JwtOptions { Issuer = issuer, Audience = audience, Secret = secret, AccessTokenMinutes = minutes };
    }

    private static string Require(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{key} is required.");
}
