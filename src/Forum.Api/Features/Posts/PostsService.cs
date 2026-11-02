using Forum.Api.Common.Authorization;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Forum.Api.Features.Posts;

public enum CreatePostFailure
{
    None,
    InvalidRequest,
    AuthorNotFound,
    Banned,
    TopicNotFound,
    TopicClosed,
    CreationFailed
}

public record CreatePostResult(CreatePostResponse? Response, CreatePostFailure Failure)
{
    public static CreatePostResult Succeeded(CreatePostResponse response) => new(response, CreatePostFailure.None);

    public static CreatePostResult Failed(CreatePostFailure failure) => new(null, failure);
}

public record PostActor(long UserId, bool CanModerate);

public enum PostManagementFailure
{
    None,
    InvalidRequest,
    NotFound,
    Forbidden,
    Deleted,
    FirstPostCannotBeDeleted
}

public class PostsService(ForumDbContext dbContext, ActiveUserBanService banService)
{
    public async Task<CreatePostResult> CreateAsync(
        long topicId,
        long authorId,
        CreatePostRequest request,
        CancellationToken cancellationToken)
    {
        var content = request.Content?.Trim() ?? string.Empty;
        if (content.Length == 0)
        {
            return CreatePostResult.Failed(CreatePostFailure.InvalidRequest);
        }

        if (await banService.IsBannedAsync(authorId, cancellationToken))
        {
            return CreatePostResult.Failed(CreatePostFailure.Banned);
        }

        if (!await dbContext.Users.AnyAsync(user => user.Id == authorId, cancellationToken))
        {
            return CreatePostResult.Failed(CreatePostFailure.AuthorNotFound);
        }

        if (!dbContext.Database.IsRelational())
        {
            return await CreateWithLockedTopicAsync(topicId, authorId, content, false, cancellationToken);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await CreateWithLockedTopicAsync(topicId, authorId, content, true, cancellationToken);
        if (result.Failure == CreatePostFailure.None)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }

    private async Task<CreatePostResult> CreateWithLockedTopicAsync(
        long topicId,
        long authorId,
        string content,
        bool lockTopic,
        CancellationToken cancellationToken)
    {
        var topic = lockTopic
            ? await dbContext.Topics.FromSqlInterpolated(
                    $"SELECT * FROM topics WHERE id = {topicId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
            : await dbContext.Topics.SingleOrDefaultAsync(topic => topic.Id == topicId, cancellationToken);
        if (topic is null || topic.DeletedAt is not null)
        {
            return CreatePostResult.Failed(CreatePostFailure.TopicNotFound);
        }

        if (topic.IsClosed)
        {
            return CreatePostResult.Failed(CreatePostFailure.TopicClosed);
        }

        var lastPosition = await dbContext.Posts
            .Where(post => post.TopicId == topicId)
            .MaxAsync(post => (int?)post.Position, cancellationToken) ?? 1;
        if (lastPosition == int.MaxValue)
        {
            return CreatePostResult.Failed(CreatePostFailure.CreationFailed);
        }

        var now = DateTime.UtcNow;
        var post = new Post
        {
            TopicId = topicId,
            AuthorId = authorId,
            Position = lastPosition + 1,
            Content = content,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Posts.Add(post);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return CreatePostResult.Succeeded(new CreatePostResponse(post.Id, post.Position));
        }
        catch (DbUpdateException)
        {
            return CreatePostResult.Failed(CreatePostFailure.CreationFailed);
        }
    }

    public async Task<PostManagementFailure> UpdateAsync(
        long topicId,
        long postId,
        PostActor actor,
        UpdatePostRequest request,
        CancellationToken cancellationToken)
    {
        var content = request.Content?.Trim() ?? string.Empty;
        if (content.Length == 0)
        {
            return PostManagementFailure.InvalidRequest;
        }

        var post = await GetPostAsync(topicId, postId, cancellationToken);
        if (post is null)
        {
            return PostManagementFailure.NotFound;
        }

        if (post.DeletedAt is not null)
        {
            return PostManagementFailure.Deleted;
        }

        if (!CanManage(post, actor))
        {
            return PostManagementFailure.Forbidden;
        }

        post.Content = content;
        await dbContext.SaveChangesAsync(cancellationToken);
        return PostManagementFailure.None;
    }

    public async Task<PostManagementFailure> DeleteAsync(
        long topicId,
        long postId,
        PostActor actor,
        CancellationToken cancellationToken)
    {
        var post = await GetPostAsync(topicId, postId, cancellationToken);
        if (post is null)
        {
            return PostManagementFailure.NotFound;
        }

        if (post.DeletedAt is not null)
        {
            return PostManagementFailure.Deleted;
        }

        if (post.Position == 1)
        {
            return PostManagementFailure.FirstPostCannotBeDeleted;
        }

        if (!CanManage(post, actor))
        {
            return PostManagementFailure.Forbidden;
        }

        post.DeletedAt = DateTime.UtcNow;
        post.DeletedBy = actor.UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return PostManagementFailure.None;
    }

    private Task<Post?> GetPostAsync(long topicId, long postId, CancellationToken cancellationToken) =>
        dbContext.Posts.SingleOrDefaultAsync(post => post.Id == postId && post.TopicId == topicId, cancellationToken);

    private static bool CanManage(Post post, PostActor actor) =>
        actor.CanModerate || post.AuthorId == actor.UserId;
}
