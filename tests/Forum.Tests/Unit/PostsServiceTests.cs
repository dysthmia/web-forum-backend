using Forum.Api.Common.Authorization;
using Forum.Api.Features.Posts;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Forum.Tests.Unit;

public class PostsServiceTests
{
    [Fact]
    public async Task CreateAsync_AssignsNextPositionAfterFirstPost()
    {
        await using var dbContext = CreateDbContext();
        var (topic, author) = await SeedTopicAsync(dbContext);
        dbContext.Posts.Add(CreatePost(topic.Id, author.Id, 1, "First post"));
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext).CreateAsync(topic.Id, author.Id,
            new CreatePostRequest { Content = " Reply " }, CancellationToken.None);

        Assert.Equal(CreatePostFailure.None, result.Failure);
        Assert.Equal(2, result.Response!.Position);
        var post = await dbContext.Posts.SingleAsync(item => item.Id == result.Response.PostId);
        Assert.Equal("Reply", post.Content);
        Assert.Equal(2, post.Position);
    }

    [Fact]
    public async Task CreateAsync_RejectsClosedTopic()
    {
        await using var dbContext = CreateDbContext();
        var (topic, author) = await SeedTopicAsync(dbContext, isClosed: true);

        var result = await CreateService(dbContext).CreateAsync(topic.Id, author.Id,
            new CreatePostRequest { Content = "Reply" }, CancellationToken.None);

        Assert.Equal(CreatePostFailure.TopicClosed, result.Failure);
        Assert.Empty(dbContext.Posts);
    }

    [Fact]
    public async Task CreateAsync_RejectsDeletedTopic()
    {
        await using var dbContext = CreateDbContext();
        var (topic, author) = await SeedTopicAsync(dbContext);
        topic.DeletedAt = DateTime.UtcNow;
        topic.DeletedBy = author.Id;
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext).CreateAsync(topic.Id, author.Id,
            new CreatePostRequest { Content = "Reply" }, CancellationToken.None);

        Assert.Equal(CreatePostFailure.TopicNotFound, result.Failure);
        Assert.Empty(dbContext.Posts);
    }

    [Fact]
    public async Task CreateAsync_RejectsBannedAuthor()
    {
        await using var dbContext = CreateDbContext();
        var (topic, author) = await SeedTopicAsync(dbContext);
        var moderator = CreateUser("moderator", "moderator@example.com");
        dbContext.Users.Add(moderator);
        await dbContext.SaveChangesAsync();
        dbContext.UserBans.Add(new UserBan
        {
            UserId = author.Id,
            ModeratorId = moderator.Id,
            Reason = "Test ban",
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext).CreateAsync(topic.Id, author.Id,
            new CreatePostRequest { Content = "Reply" }, CancellationToken.None);

        Assert.Equal(CreatePostFailure.Banned, result.Failure);
        Assert.Empty(dbContext.Posts);
    }

    private static PostsService CreateService(ForumDbContext dbContext) =>
        new(dbContext, new ActiveUserBanService(dbContext));

    private static async Task<(Topic Topic, User Author)> SeedTopicAsync(ForumDbContext dbContext, bool isClosed = false)
    {
        var category = new Category { Name = "General", Slug = "general" };
        var author = CreateUser("author", "author@example.com");
        dbContext.AddRange(category, author);
        await dbContext.SaveChangesAsync();
        var topic = new Topic
        {
            CategoryId = category.Id,
            AuthorId = author.Id,
            Title = "Topic",
            IsClosed = isClosed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Topics.Add(topic);
        await dbContext.SaveChangesAsync();
        return (topic, author);
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

    private static Post CreatePost(long topicId, long authorId, int position, string content) => new()
    {
        TopicId = topicId,
        AuthorId = authorId,
        Position = position,
        Content = content,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
