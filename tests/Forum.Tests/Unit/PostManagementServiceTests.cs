using Forum.Api.Common.Authorization;
using Forum.Api.Features.Posts;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Forum.Tests.Unit;

public class PostManagementServiceTests
{
    [Fact]
    public async Task UpdateAsync_AllowsAuthorInClosedTopicButRejectsAnotherUser()
    {
        await using var dbContext = CreateDbContext();
        var (topic, post, author, otherUser) = await SeedPostAsync(dbContext, position: 2, isClosed: true);
        var service = CreateService(dbContext);

        var rejected = await service.UpdateAsync(topic.Id, post.Id, new PostActor(otherUser.Id, false),
            new UpdatePostRequest { Content = "Changed" }, CancellationToken.None);
        var updated = await service.UpdateAsync(topic.Id, post.Id, new PostActor(author.Id, false),
            new UpdatePostRequest { Content = " Changed " }, CancellationToken.None);

        Assert.Equal(PostManagementFailure.Forbidden, rejected);
        Assert.Equal(PostManagementFailure.None, updated);
        Assert.Equal("Changed", (await dbContext.Posts.SingleAsync()).Content);
    }

    [Fact]
    public async Task UpdateAsync_AllowsModeratorToEditAnotherUsersPost()
    {
        await using var dbContext = CreateDbContext();
        var (topic, post, _, moderator) = await SeedPostAsync(dbContext, position: 2);

        var result = await CreateService(dbContext).UpdateAsync(topic.Id, post.Id, new PostActor(moderator.Id, true),
            new UpdatePostRequest { Content = "Moderator edit" }, CancellationToken.None);

        Assert.Equal(PostManagementFailure.None, result);
        Assert.Equal("Moderator edit", (await dbContext.Posts.SingleAsync()).Content);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesReplyAndPreventsFurtherEditing()
    {
        await using var dbContext = CreateDbContext();
        var (topic, post, author, _) = await SeedPostAsync(dbContext, position: 2);
        var service = CreateService(dbContext);

        var deleted = await service.DeleteAsync(topic.Id, post.Id, new PostActor(author.Id, false), CancellationToken.None);
        var update = await service.UpdateAsync(topic.Id, post.Id, new PostActor(author.Id, false),
            new UpdatePostRequest { Content = "Changed" }, CancellationToken.None);

        var persistedPost = await dbContext.Posts.SingleAsync();
        Assert.Equal(PostManagementFailure.None, deleted);
        Assert.NotNull(persistedPost.DeletedAt);
        Assert.Equal(author.Id, persistedPost.DeletedBy);
        Assert.Equal(PostManagementFailure.Deleted, update);
    }

    [Fact]
    public async Task DeleteAsync_RejectsFirstPost()
    {
        await using var dbContext = CreateDbContext();
        var (topic, post, author, _) = await SeedPostAsync(dbContext, position: 1);

        var result = await CreateService(dbContext).DeleteAsync(topic.Id, post.Id,
            new PostActor(author.Id, false), CancellationToken.None);

        Assert.Equal(PostManagementFailure.FirstPostCannotBeDeleted, result);
        Assert.Null((await dbContext.Posts.SingleAsync()).DeletedAt);
    }

    private static PostsService CreateService(ForumDbContext dbContext) =>
        new(dbContext, new ActiveUserBanService(dbContext));

    private static async Task<(Topic Topic, Post Post, User Author, User OtherUser)> SeedPostAsync(
        ForumDbContext dbContext,
        int position,
        bool isClosed = false)
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
            Title = "Topic",
            IsClosed = isClosed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Topics.Add(topic);
        await dbContext.SaveChangesAsync();
        var post = new Post
        {
            TopicId = topic.Id,
            AuthorId = author.Id,
            Position = position,
            Content = "Original",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Posts.Add(post);
        await dbContext.SaveChangesAsync();
        return (topic, post, author, otherUser);
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
