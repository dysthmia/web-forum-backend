using Forum.Api.Features.Auth;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Forum.Tests.Unit;

public class PasswordServiceTests
{
    [Fact]
    public void HashPassword_StoresSaltedHashAndVerifiesOnlyCorrectPassword()
    {
        var service = new PasswordService(new PasswordHasher<User>());
        var user = new User();

        var hash = service.HashPassword(user, "correct-password");
        user.PasswordHash = hash;

        Assert.NotEqual("correct-password", hash);
        Assert.True(service.VerifyPassword(user, "correct-password"));
        Assert.False(service.VerifyPassword(user, "wrong-password"));
        Assert.False(service.VerifyPassword(null, "correct-password"));
    }
}
