using Forum.Api.Common.Authorization;
using Forum.Api.Features.Bookmarks;
using Forum.Api.Features.Topics;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Forum.Tests.Unit;

public class BookmarksServiceTests
{
    [Fact]
    public async Task AddAsync_IsIdempotentAndRemoveAsync_DeletesOnlyCurrentUsersBookmark()
    {
        await using var dbContext = CreateDbContext();
        var (topic, user, otherUser) = await SeedAsync(dbContext);
        var service = CreateService(dbContext);

        var firstAdd = await service.AddAsync(user.Id, topic.Id, CancellationToken.None);
        var repeatedAdd = await service.AddAsync(user.Id, topic.Id, CancellationToken.None);
        var otherAdd = await service.AddAsync(otherUser.Id, topic.Id, CancellationToken.None);
        var remove = await service.RemoveAsync(user.Id, topic.Id, CancellationToken.None);

        Assert.Equal(BookmarkFailure.None, firstAdd);
        Assert.Equal(BookmarkFailure.None, repeatedAdd);
        Assert.Equal(BookmarkFailure.None, otherAdd);
        Assert.Equal(BookmarkFailure.None, remove);
        var bookmark = await dbContext.Bookmarks.SingleAsync();
        Assert.Equal(otherUser.Id, bookmark.UserId);
    }

    [Fact]
    public async Task GetListAsync_ReturnsOnlyCurrentUsersVisibleTopics()
    {
        await using var dbContext = CreateDbContext();
        var (visibleTopic, user, otherUser) = await SeedAsync(dbContext);
        var deletedTopic = new Topic
        {
            CategoryId = visibleTopic.CategoryId,
            AuthorId = user.Id,
            Title = "Deleted",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            DeletedAt = DateTime.UtcNow
        };
        dbContext.Topics.Add(deletedTopic);
        await dbContext.SaveChangesAsync();
        dbContext.Bookmarks.AddRange(
            new Bookmark { UserId = user.Id, TopicId = visibleTopic.Id, CreatedAt = DateTime.UtcNow },
            new Bookmark { UserId = user.Id, TopicId = deletedTopic.Id, CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new Bookmark { UserId = otherUser.Id, TopicId = visibleTopic.Id, CreatedAt = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext).GetListAsync(user.Id,
            new BookmarkPageRequest { PageSize = 10 }, CancellationToken.None);

        Assert.Equal(BookmarkFailure.None, result.Failure);
        Assert.Equal(1, result.Response!.TotalCount);
        Assert.Single(result.Response.Items);
        Assert.Equal(visibleTopic.Id, result.Response.Items[0].TopicId);
        Assert.Equal(3, await dbContext.Bookmarks.CountAsync());
    }

    [Fact]
    public async Task AddAsync_RejectsDeletedTopic()
    {
        await using var dbContext = CreateDbContext();
        var (topic, user, _) = await SeedAsync(dbContext);
        topic.DeletedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext).AddAsync(user.Id, topic.Id, CancellationToken.None);

        Assert.Equal(BookmarkFailure.TopicNotFound, result);
        Assert.Empty(dbContext.Bookmarks);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsCurrentUsersBookmarkState()
    {
        await using var dbContext = CreateDbContext();
        var (topic, user, otherUser) = await SeedAsync(dbContext);
        dbContext.Bookmarks.Add(new Bookmark { UserId = otherUser.Id, TopicId = topic.Id, CreatedAt = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var notBookmarked = await service.GetStatusAsync(user.Id, topic.Id, CancellationToken.None);
        await service.AddAsync(user.Id, topic.Id, CancellationToken.None);
        var bookmarked = await service.GetStatusAsync(user.Id, topic.Id, CancellationToken.None);

        Assert.False(notBookmarked.Response!.IsBookmarked);
        Assert.True(bookmarked.Response!.IsBookmarked);
    }

    private static BookmarksService CreateService(ForumDbContext dbContext) => new(
        dbContext,
        new ActiveUserBanService(dbContext),
        new TopicPaginationOptions { DefaultPageSize = 2, MaxPageSize = 10 });

    private static async Task<(Topic Topic, User User, User OtherUser)> SeedAsync(ForumDbContext dbContext)
    {
        var category = new Category { Name = "General", Slug = "general" };
        var user = CreateUser("user", "user@example.com");
        var otherUser = CreateUser("other", "other@example.com");
        dbContext.AddRange(category, user, otherUser);
        await dbContext.SaveChangesAsync();
        var topic = new Topic
        {
            CategoryId = category.Id,
            AuthorId = user.Id,
            Title = "Visible",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Topics.Add(topic);
        await dbContext.SaveChangesAsync();
        return (topic, user, otherUser);
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
