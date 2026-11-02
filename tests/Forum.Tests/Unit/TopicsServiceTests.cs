using Forum.Api.Common.Authorization;
using Forum.Api.Features.Topics;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Forum.Tests.Unit;

public class TopicsServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesTopicAndFirstPost()
    {
        await using var dbContext = CreateDbContext();
        var author = CreateUser("author", "author@example.com");
        var category = new Category { Name = "General", Slug = "general" };
        dbContext.AddRange(author, category);
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext).CreateAsync(author.Id, new CreateTopicRequest
        {
            CategoryId = category.Id,
            Title = " First topic ",
            Content = " First post "
        }, CancellationToken.None);

        var topic = await dbContext.Topics.SingleAsync();
        var post = await dbContext.Posts.SingleAsync();
        Assert.Equal(CreateTopicFailure.None, result.Failure);
        Assert.Equal(topic.Id, result.Response!.TopicId);
        Assert.Equal("First topic", topic.Title);
        Assert.Equal(topic.Id, post.TopicId);
        Assert.Equal(author.Id, post.AuthorId);
        Assert.Equal(1, post.Position);
        Assert.Equal("First post", post.Content);
    }

    [Fact]
    public async Task CreateAsync_DoesNotCreateRecords_WhenCategoryDoesNotExist()
    {
        await using var dbContext = CreateDbContext();
        var author = CreateUser("author", "author@example.com");
        dbContext.Users.Add(author);
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext).CreateAsync(author.Id, new CreateTopicRequest
        {
            CategoryId = 999,
            Title = "Topic",
            Content = "First post"
        }, CancellationToken.None);

        Assert.Equal(CreateTopicFailure.CategoryNotFound, result.Failure);
        Assert.Empty(dbContext.Topics);
        Assert.Empty(dbContext.Posts);
    }

    [Fact]
    public async Task CreateAsync_RejectsBannedAuthor()
    {
        await using var dbContext = CreateDbContext();
        var author = CreateUser("author", "author@example.com");
        var moderator = CreateUser("moderator", "moderator@example.com");
        var category = new Category { Name = "General", Slug = "general" };
        dbContext.AddRange(author, moderator, category);
        await dbContext.SaveChangesAsync();
        dbContext.UserBans.Add(new UserBan
        {
            UserId = author.Id,
            ModeratorId = moderator.Id,
            Reason = "Test ban",
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext).CreateAsync(author.Id, new CreateTopicRequest
        {
            CategoryId = category.Id,
            Title = "Topic",
            Content = "First post"
        }, CancellationToken.None);

        Assert.Equal(CreateTopicFailure.Banned, result.Failure);
        Assert.Empty(dbContext.Topics);
        Assert.Empty(dbContext.Posts);
    }

    private static TopicsService CreateService(ForumDbContext dbContext) =>
        new(dbContext, new ActiveUserBanService(dbContext));

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
