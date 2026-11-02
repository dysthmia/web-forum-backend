using Forum.Api.Common.Authorization;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Forum.Api.Features.Reactions;

public enum ReactionFailure
{
    None,
    InvalidRequest,
    AuthorNotFound,
    Banned,
    PostNotFound
}

public record ReactionResult(ReactionResponse? Response, ReactionFailure Failure)
{
    public static ReactionResult Succeeded(ReactionResponse response) => new(response, ReactionFailure.None);

    public static ReactionResult Failed(ReactionFailure failure) => new(null, failure);
}

public class ReactionsService(ForumDbContext dbContext, ActiveUserBanService banService)
{
    public async Task<ReactionResult> SetAsync(
        long postId,
        long userId,
        SetReactionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Type != (int)ReactionType.Like && request.Type != (int)ReactionType.Dislike)
        {
            return ReactionResult.Failed(ReactionFailure.InvalidRequest);
        }

        if (await banService.IsBannedAsync(userId, cancellationToken))
        {
            return ReactionResult.Failed(ReactionFailure.Banned);
        }

        if (!await dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return ReactionResult.Failed(ReactionFailure.AuthorNotFound);
        }

        if (!await IsVisiblePostAsync(postId, cancellationToken))
        {
            return ReactionResult.Failed(ReactionFailure.PostNotFound);
        }

        var type = (ReactionType)request.Type;
        var reaction = await dbContext.Reactions.SingleOrDefaultAsync(
            item => item.UserId == userId && item.PostId == postId,
            cancellationToken);
        if (reaction is not null)
        {
            if (reaction.Type != type)
            {
                reaction.Type = type;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return ReactionResult.Succeeded(new ReactionResponse(postId, (int)type));
        }

        dbContext.Reactions.Add(new Reaction
        {
            UserId = userId,
            PostId = postId,
            Type = type,
            CreatedAt = DateTime.UtcNow
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ReactionResult.Succeeded(new ReactionResponse(postId, (int)type));
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            dbContext.ChangeTracker.Clear();
            return await SetAfterConflictAsync(postId, userId, type, cancellationToken);
        }
    }

    public async Task<ReactionFailure> RemoveAsync(long postId, long userId, CancellationToken cancellationToken)
    {
        if (await banService.IsBannedAsync(userId, cancellationToken))
        {
            return ReactionFailure.Banned;
        }

        if (!await dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return ReactionFailure.AuthorNotFound;
        }

        if (!await IsVisiblePostAsync(postId, cancellationToken))
        {
            return ReactionFailure.PostNotFound;
        }

        var reaction = await dbContext.Reactions.SingleOrDefaultAsync(
            item => item.UserId == userId && item.PostId == postId,
            cancellationToken);
        if (reaction is not null)
        {
            dbContext.Reactions.Remove(reaction);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ReactionFailure.None;
    }

    public async Task<IReadOnlyList<ReactionCountsResponse>> GetCountsAsync(
        IReadOnlyCollection<long> postIds,
        CancellationToken cancellationToken)
    {
        var ids = postIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        var counts = await dbContext.Reactions.AsNoTracking()
            .Where(reaction => ids.Contains(reaction.PostId)
                               && reaction.Post.DeletedAt == null
                               && reaction.Post.Topic.DeletedAt == null)
            .GroupBy(reaction => reaction.PostId)
            .Select(group => new ReactionCountsResponse(
                group.Key,
                group.Count(reaction => reaction.Type == ReactionType.Like),
                group.Count(reaction => reaction.Type == ReactionType.Dislike)))
            .ToDictionaryAsync(item => item.PostId, cancellationToken);

        return ids.Select(postId => counts.GetValueOrDefault(postId, new ReactionCountsResponse(postId, 0, 0))).ToArray();
    }

    private async Task<ReactionResult> SetAfterConflictAsync(
        long postId,
        long userId,
        ReactionType type,
        CancellationToken cancellationToken)
    {
        var reaction = await dbContext.Reactions.SingleAsync(
            item => item.UserId == userId && item.PostId == postId,
            cancellationToken);
        if (reaction.Type != type)
        {
            reaction.Type = type;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ReactionResult.Succeeded(new ReactionResponse(postId, (int)type));
    }

    private Task<bool> IsVisiblePostAsync(long postId, CancellationToken cancellationToken) =>
        dbContext.Posts.AsNoTracking().AnyAsync(
            post => post.Id == postId && post.DeletedAt == null && post.Topic.DeletedAt == null,
            cancellationToken);
}
