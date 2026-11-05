using Forum.Api.Features.Users;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace Forum.Tests.Unit;

public class UsersServiceTests
{
    [Fact]
    public async Task GetPublicProfileAsync_DoesNotExposePersonalFields_AndExcludesDeletedPosts()
    {
        await using var dbContext = CreateDbContext();
        var user = new User
        {
            Username = "public-user",
            Email = "public@example.com",
            PasswordHash = "secret-hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };
        var category = new Category { Name = "General", Slug = "general" };
        dbContext.AddRange(user, category);
        await dbContext.SaveChangesAsync();

        var topic = new Topic
        {
            CategoryId = category.Id,
            AuthorId = user.Id,
            Title = "A topic",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Topics.Add(topic);
        await dbContext.SaveChangesAsync();
        dbContext.Posts.AddRange(
            new Post
            {
                TopicId = topic.Id,
                AuthorId = user.Id,
                Position = 1,
                Content = "Visible post",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Post
            {
                TopicId = topic.Id,
                AuthorId = user.Id,
                Position = 2,
                Content = "Deleted post",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                DeletedAt = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var result = await new UsersService(dbContext).GetPublicProfileAsync(user.Id, CancellationToken.None);

        Assert.Equal(UserProfileFailure.None, result.Failure);
        Assert.NotNull(result.PublicProfile);
        Assert.Equal("public-user", result.PublicProfile.Username);
        Assert.Equal(1, result.PublicProfile.PostCount);
        Assert.DoesNotContain(typeof(PublicUserProfileResponse).GetProperties(), property =>
            property.Name is "Email" or "PasswordHash" or "RefreshToken");
    }

    [Fact]
    public async Task GetPublicProfileByUsernameAsync_FindsUserIgnoringCase()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser("Alice", "alice@example.com");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var result = await new UsersService(dbContext).GetPublicProfileByUsernameAsync(
            "alice",
            CancellationToken.None);

        Assert.Equal(UserProfileFailure.None, result.Failure);
        Assert.NotNull(result.PublicProfile);
        Assert.Equal(user.Id, result.PublicProfile.Id);
        Assert.Equal("Alice", result.PublicProfile.Username);
    }

    [Fact]
    public async Task UpdateOwnProfileAsync_ReturnsConflict_WhenUsernameBelongsToAnotherUser()
    {
        await using var dbContext = CreateDbContext();
        var currentUser = CreateUser("current-user", "current@example.com");
        var otherUser = CreateUser("taken-user", "taken@example.com");
        dbContext.Users.AddRange(currentUser, otherUser);
        await dbContext.SaveChangesAsync();

        var result = await new UsersService(dbContext).UpdateOwnProfileAsync(
            currentUser.Id,
            new UpdateOwnProfileRequest { Username = otherUser.Username },
            CancellationToken.None);

        Assert.Equal(UserProfileFailure.DuplicateUser, result.Failure);
        Assert.Equal("current-user", currentUser.Username);
    }

    [Fact]
    public async Task UpdateOwnProfileAsync_ChangesOnlySuppliedFields()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser("old-name", "old@example.com");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var result = await new UsersService(dbContext).UpdateOwnProfileAsync(
            user.Id,
            new UpdateOwnProfileRequest { Email = "new@example.com" },
            CancellationToken.None);

        Assert.Equal(UserProfileFailure.None, result.Failure);
        Assert.NotNull(result.OwnProfile);
        Assert.Equal("old-name", result.OwnProfile.Username);
        Assert.Equal("new@example.com", result.OwnProfile.Email);
    }

    [Fact]
    public async Task UpdateOwnProfile_ReturnsConflict_WhenClaimedUserChoosesTakenUsername()
    {
        await using var dbContext = CreateDbContext();
        var currentUser = CreateUser("current-user", "current@example.com");
        var otherUser = CreateUser("taken-user", "taken@example.com");
        dbContext.Users.AddRange(currentUser, otherUser);
        await dbContext.SaveChangesAsync();
        var controller = new UsersController(new UsersService(dbContext))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, currentUser.Id.ToString())],
                        "test"))
                }
            }
        };

        var result = await controller.UpdateOwnProfile(
            new UpdateOwnProfileRequest { Username = otherUser.Username },
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("current-user", currentUser.Username);
    }

    private static ForumDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ForumDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ForumDbContext(options);
    }

    private static User CreateUser(string username, string email) => new()
    {
        Username = username,
        Email = email,
        PasswordHash = "hash",
        Role = UserRole.User,
        CreatedAt = DateTime.UtcNow
    };
}
