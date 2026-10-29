using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Forum.Api.Features.Auth;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Forum.Tests.Unit;

public class JwtTokenServiceTests
{
    [Fact]
    public void CreateAccessToken_CreatesValidSignedTokenWithUserClaims()
    {
        var options = new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            Secret = "01234567890123456789012345678901",
            AccessTokenMinutes = 15
        };
        var token = new JwtTokenService(options).CreateAccessToken(new User { Id = 42, Role = UserRole.Moderator });
        var handler = new JwtSecurityTokenHandler();

        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret)),
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        }, out _);

        Assert.Equal("42", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.True(principal.IsInRole("moderator"));
    }

    [Fact]
    public void Validation_RejectsExpiredToken()
    {
        var secret = "01234567890123456789012345678901";
        var handler = new JwtSecurityTokenHandler();
        var expiredToken = handler.WriteToken(new JwtSecurityToken(
            "test-issuer",
            "test-audience",
            expires: DateTime.UtcNow.AddMinutes(-1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256)));

        Assert.Throws<SecurityTokenExpiredException>(() => handler.ValidateToken(expiredToken, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer = true,
            ValidIssuer = "test-issuer",
            ValidateAudience = true,
            ValidAudience = "test-audience",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        }, out _));
    }
}
