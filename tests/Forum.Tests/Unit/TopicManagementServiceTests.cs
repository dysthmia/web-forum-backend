using Forum.Api.Common.Authorization;
using Forum.Api.Features.Topics;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Forum.Tests.Unit;

public class TopicManagementServiceTests
{
    [Fact]
    public async Task UpdateTitleAsync_AllowsAuthorButRejectsAnotherUser()
    {
        await using var dbContext = CreateDbContext();
        var (topic, author, otherUser) = await SeedTopicAsync(dbContext);
        var service = CreateService(dbContext);

        var rejected = await service.UpdateTitleAsync(topic.Id, new TopicActor(otherUser.Id, false),
            new UpdateTopicRequest { Title = "Changed" }, CancellationToken.None);
        var updated = await service.UpdateTitleAsync(topic.Id, new TopicActor(author.Id, false),
            new UpdateTopicRequest { Title = "Changed" }, CancellationToken.None);

        Assert.Equal(TopicManagementFailure.Forbidden, rejected);
        Assert.Equal(TopicManagementFailure.None, updated);
        Assert.Equal("Changed", (await dbContext.Topics.SingleAsync()).Title);
    }

    [Fact]
    public async Task Moderator_CanCloseAndPinAnyTopic()
    {
        await using var dbContext = CreateDbContext();
        var (topic, _, otherUser) = await SeedTopicAsync(dbContext);
        var service = CreateService(dbContext);
        var moderator = new TopicActor(otherUser.Id, true);

        var rejected = await service.SetClosedAsync(topic.Id, new TopicActor(otherUser.Id, false),
            new SetTopicClosedRequest { IsClosed = true }, CancellationToken.None);
        var close = await service.SetClosedAsync(topic.Id, moderator,
            new SetTopicClosedRequest { IsClosed = true }, CancellationToken.None);
        var pin = await service.SetPinnedAsync(topic.Id, moderator,
            new SetTopicPinnedRequest { IsPinned = true }, CancellationToken.None);

        var changedTopic = await dbContext.Topics.SingleAsync();
        Assert.Equal(TopicManagementFailure.Forbidden, rejected);
        Assert.Equal(TopicManagementFailure.None, close);
        Assert.Equal(TopicManagementFailure.None, pin);
        Assert.True(changedTopic.IsClosed);
        Assert.True(changedTopic.IsPinned);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesTopicWithoutDeletingPosts_AndHidesItFromPublicDetails()
    {
        await using var dbContext = CreateDbContext();
        var (topic, author, _) = await SeedTopicAsync(dbContext, includeFirstPost: true);
        var service = CreateService(dbContext);

        var result = await service.DeleteAsync(topic.Id, new TopicActor(author.Id, false), CancellationToken.None);

        var deletedTopic = await dbContext.Topics.SingleAsync();
        var publicResult = await service.GetDetailsAsync(topic.Id, new TopicPageRequest(), CancellationToken.None);
        Assert.Equal(TopicManagementFailure.None, result);
        Assert.NotNull(deletedTopic.DeletedAt);
        Assert.Equal(author.Id, deletedTopic.DeletedBy);
        Assert.Single(dbContext.Posts);
        Assert.Equal(TopicReadFailure.TopicNotFound, publicResult.Failure);
    }

    private static TopicsService CreateService(ForumDbContext dbContext) => new(
        dbContext,
        new ActiveUserBanService(dbContext),
        new TopicPaginationOptions { DefaultPageSize = 10, MaxPageSize = 50 });

    private static async Task<(Topic Topic, User Author, User OtherUser)> SeedTopicAsync(
        ForumDbContext dbContext,
        bool includeFirstPost = false)
    {
        var category = new Category { Name = "General", Slug = "general" };
        var author = CreateUser("author", "author@example.com");
        var otherUser = CreateUser("other", "other@example.com");
        dbContext.AddRange(category, author, otherUser);
        await dbContext.SaveChangesAsync();
        var topic = new Topic
        {
            CategoryId = category.Id,
            AuthorId = author.Id,
            Title = "Original",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Topics.Add(topic);
        await dbContext.SaveChangesAsync();

        if (includeFirstPost)
        {
            dbContext.Posts.Add(new Post
            {
                TopicId = topic.Id,
                AuthorId = author.Id,
                Position = 1,
                Content = "First post",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        return (topic, author, otherUser);
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
