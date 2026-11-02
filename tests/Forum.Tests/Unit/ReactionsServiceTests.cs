using Forum.Api.Common.Authorization;
using Forum.Api.Features.Reactions;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Forum.Tests.Unit;

public class ReactionsServiceTests
{
    [Fact]
    public async Task SetAsync_IsIdempotentForSameReaction_AndSwitchesType()
    {
        await using var dbContext = CreateDbContext();
        var (post, user, _) = await SeedPostAsync(dbContext);
        var service = CreateService(dbContext);

        var first = await service.SetAsync(post.Id, user.Id, new SetReactionRequest { Type = 1 }, CancellationToken.None);
        var repeated = await service.SetAsync(post.Id, user.Id, new SetReactionRequest { Type = 1 }, CancellationToken.None);
        var switched = await service.SetAsync(post.Id, user.Id, new SetReactionRequest { Type = -1 }, CancellationToken.None);

        Assert.Equal(ReactionFailure.None, first.Failure);
        Assert.Equal(ReactionFailure.None, repeated.Failure);
        Assert.Equal(ReactionFailure.None, switched.Failure);
        var reaction = await dbContext.Reactions.SingleAsync();
        Assert.Equal(ReactionType.Dislike, reaction.Type);
    }

    [Fact]
    public async Task RemoveAsync_DeletesReaction()
    {
        await using var dbContext = CreateDbContext();
        var (post, user, _) = await SeedPostAsync(dbContext);
        var service = CreateService(dbContext);
        await service.SetAsync(post.Id, user.Id, new SetReactionRequest { Type = 1 }, CancellationToken.None);

        var failure = await service.RemoveAsync(post.Id, user.Id, CancellationToken.None);

        Assert.Equal(ReactionFailure.None, failure);
        Assert.Empty(dbContext.Reactions);
    }

    [Fact]
    public async Task SetAsync_RejectsDeletedPostAndDeletedTopic()
    {
        await using var dbContext = CreateDbContext();
        var (post, user, topic) = await SeedPostAsync(dbContext);
        var service = CreateService(dbContext);
        post.DeletedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var deletedPostResult = await service.SetAsync(post.Id, user.Id,
            new SetReactionRequest { Type = 1 }, CancellationToken.None);

        post.DeletedAt = null;
        topic.DeletedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();
        var deletedTopicResult = await service.SetAsync(post.Id, user.Id,
            new SetReactionRequest { Type = 1 }, CancellationToken.None);

        Assert.Equal(ReactionFailure.PostNotFound, deletedPostResult.Failure);
        Assert.Equal(ReactionFailure.PostNotFound, deletedTopicResult.Failure);
    }

    [Fact]
    public async Task GetCountsAsync_ReturnsLikeAndDislikeCountsInOneBatch()
    {
        await using var dbContext = CreateDbContext();
        var (firstPost, firstUser, topic) = await SeedPostAsync(dbContext);
        var secondUser = CreateUser("second", "second@example.com");
        dbContext.Users.Add(secondUser);
        var secondPost = new Post
        {
            TopicId = topic.Id,
            AuthorId = firstUser.Id,
            Position = 2,
            Content = "Second",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Posts.Add(secondPost);
        await dbContext.SaveChangesAsync();
        dbContext.Reactions.AddRange(
            new Reaction { UserId = firstUser.Id, PostId = firstPost.Id, Type = ReactionType.Like, CreatedAt = DateTime.UtcNow },
            new Reaction { UserId = secondUser.Id, PostId = firstPost.Id, Type = ReactionType.Dislike, CreatedAt = DateTime.UtcNow },
            new Reaction { UserId = secondUser.Id, PostId = secondPost.Id, Type = ReactionType.Like, CreatedAt = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var counts = await CreateService(dbContext).GetCountsAsync([firstPost.Id, secondPost.Id], CancellationToken.None);

        Assert.Collection(counts,
            item => { Assert.Equal(firstPost.Id, item.PostId); Assert.Equal(1, item.LikeCount); Assert.Equal(1, item.DislikeCount); },
            item => { Assert.Equal(secondPost.Id, item.PostId); Assert.Equal(1, item.LikeCount); Assert.Equal(0, item.DislikeCount); });
    }

    private static ReactionsService CreateService(ForumDbContext dbContext) =>
        new(dbContext, new ActiveUserBanService(dbContext));

    private static async Task<(Post Post, User User, Topic Topic)> SeedPostAsync(ForumDbContext dbContext)
    {
        var category = new Category { Name = "General", Slug = "general" };
        var user = CreateUser("author", "author@example.com");
        dbContext.AddRange(category, user);
        await dbContext.SaveChangesAsync();
        var topic = new Topic
        {
            CategoryId = category.Id,
            AuthorId = user.Id,
            Title = "Topic",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Topics.Add(topic);
        await dbContext.SaveChangesAsync();
        var post = new Post
        {
            TopicId = topic.Id,
            AuthorId = user.Id,
            Position = 1,
            Content = "First",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Posts.Add(post);
        await dbContext.SaveChangesAsync();
        return (post, user, topic);
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
