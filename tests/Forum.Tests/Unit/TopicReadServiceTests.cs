using Forum.Api.Common.Authorization;
using Forum.Api.Features.Topics;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Forum.Tests.Unit;

public class TopicReadServiceTests
{
    [Fact]
    public async Task GetCategoryTopicsAsync_PutsPinnedTopicsFirst_AndHidesDeletedTopics()
    {
        await using var dbContext = CreateDbContext();
        var category = new Category { Name = "General", Slug = "general" };
        var author = CreateUser();
        dbContext.AddRange(category, author);
        await dbContext.SaveChangesAsync();
        var now = DateTime.UtcNow;
        dbContext.Topics.AddRange(
            CreateTopic(category.Id, author.Id, "Regular", now.AddMinutes(-1)),
            CreateTopic(category.Id, author.Id, "Pinned", now, isPinned: true),
            new Topic
            {
                CategoryId = category.Id,
                AuthorId = author.Id,
                Title = "Deleted",
                CreatedAt = now,
                UpdatedAt = now,
                DeletedAt = now
            });
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext).GetCategoryTopicsAsync(
            category.Id,
            new GetCategoryTopicsRequest { PageSize = 3 },
            CancellationToken.None);

        Assert.Equal(TopicReadFailure.None, result.Failure);
        Assert.Equal(2, result.Response!.TotalCount);
        Assert.Collection(result.Response.Items,
            topic => Assert.Equal("Pinned", topic.Title),
            topic => Assert.Equal("Regular", topic.Title));
    }

    [Fact]
    public async Task GetDetailsAsync_ReturnsFirstPostAndOrderedActiveReplies()
    {
        await using var dbContext = CreateDbContext();
        var category = new Category { Name = "General", Slug = "general" };
        var author = CreateUser();
        dbContext.AddRange(category, author);
        await dbContext.SaveChangesAsync();
        var topic = CreateTopic(category.Id, author.Id, "Topic", DateTime.UtcNow);
        dbContext.Topics.Add(topic);
        await dbContext.SaveChangesAsync();
        dbContext.Posts.AddRange(
            CreatePost(topic.Id, author.Id, 1, "First"),
            CreatePost(topic.Id, author.Id, 3, "Third"),
            CreatePost(topic.Id, author.Id, 2, "Second"),
            new Post
            {
                TopicId = topic.Id,
                AuthorId = author.Id,
                Position = 4,
                Content = "Deleted",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                DeletedAt = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext).GetDetailsAsync(
            topic.Id,
            new TopicPageRequest { PageSize = 3 },
            CancellationToken.None);

        Assert.Equal(TopicReadFailure.None, result.Failure);
        Assert.Equal(1, result.Response!.FirstPost.Position);
        Assert.Equal(2, result.Response.Replies.TotalCount);
        Assert.Collection(result.Response.Replies.Items,
            post => Assert.Equal(2, post.Position),
            post => Assert.Equal(3, post.Position));
    }

    private static TopicsService CreateService(ForumDbContext dbContext) => new(
        dbContext,
        new ActiveUserBanService(dbContext),
        new TopicPaginationOptions { DefaultPageSize = 2, MaxPageSize = 3 });

    private static ForumDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ForumDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ForumDbContext(options);
    }

    private static User CreateUser() => new()
    {
        Username = "author",
        Email = "author@example.com",
        PasswordHash = "hash",
        Role = UserRole.User,
        CreatedAt = DateTime.UtcNow
    };

    private static Topic CreateTopic(long categoryId, long authorId, string title, DateTime createdAt, bool isPinned = false) => new()
    {
        CategoryId = categoryId,
        AuthorId = authorId,
        Title = title,
        IsPinned = isPinned,
        CreatedAt = createdAt,
        UpdatedAt = createdAt
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
