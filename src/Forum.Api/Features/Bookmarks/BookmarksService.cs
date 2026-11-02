using Forum.Api.Common.Authorization;
using Forum.Api.Features.Topics;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Forum.Api.Features.Bookmarks;

public enum BookmarkFailure
{
    None,
    InvalidRequest,
    UserNotFound,
    Banned,
    TopicNotFound
}

public record BookmarkListResult(BookmarkListResponse? Response, BookmarkFailure Failure)
{
    public static BookmarkListResult Succeeded(BookmarkListResponse response) => new(response, BookmarkFailure.None);

    public static BookmarkListResult Failed(BookmarkFailure failure) => new(null, failure);
}

public class BookmarksService(
    ForumDbContext dbContext,
    ActiveUserBanService banService,
    TopicPaginationOptions paginationOptions)
{
    public async Task<BookmarkFailure> AddAsync(long userId, long topicId, CancellationToken cancellationToken)
    {
        var accessFailure = await GetAccessFailureAsync(userId, cancellationToken);
        if (accessFailure != BookmarkFailure.None)
        {
            return accessFailure;
        }

        if (!await dbContext.Topics.AsNoTracking().AnyAsync(topic => topic.Id == topicId && topic.DeletedAt == null, cancellationToken))
        {
            return BookmarkFailure.TopicNotFound;
        }

        if (await dbContext.Bookmarks.AnyAsync(
                bookmark => bookmark.UserId == userId && bookmark.TopicId == topicId,
                cancellationToken))
        {
            return BookmarkFailure.None;
        }

        dbContext.Bookmarks.Add(new Bookmark
        {
            UserId = userId,
            TopicId = topicId,
            CreatedAt = DateTime.UtcNow
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return BookmarkFailure.None;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            dbContext.ChangeTracker.Clear();
            return BookmarkFailure.None;
        }
    }

    public async Task<BookmarkFailure> RemoveAsync(long userId, long topicId, CancellationToken cancellationToken)
    {
        var accessFailure = await GetAccessFailureAsync(userId, cancellationToken);
        if (accessFailure != BookmarkFailure.None)
        {
            return accessFailure;
        }

        var bookmark = await dbContext.Bookmarks.SingleOrDefaultAsync(
            item => item.UserId == userId && item.TopicId == topicId,
            cancellationToken);
        if (bookmark is not null)
        {
            dbContext.Bookmarks.Remove(bookmark);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return BookmarkFailure.None;
    }

    public async Task<BookmarkListResult> GetListAsync(
        long userId,
        BookmarkPageRequest request,
        CancellationToken cancellationToken)
    {
        var accessFailure = await GetAccessFailureAsync(userId, cancellationToken);
        if (accessFailure != BookmarkFailure.None)
        {
            return BookmarkListResult.Failed(accessFailure);
        }

        if (!TryGetPage(request, out var page, out var pageSize, out var skip))
        {
            return BookmarkListResult.Failed(BookmarkFailure.InvalidRequest);
        }

        var query = dbContext.Bookmarks.AsNoTracking()
            .Where(bookmark => bookmark.UserId == userId && bookmark.Topic.DeletedAt == null);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(bookmark => bookmark.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .Select(bookmark => new BookmarkTopicResponse(
                bookmark.TopicId,
                bookmark.Topic.Title,
                bookmark.Topic.IsClosed,
                bookmark.Topic.IsPinned,
                bookmark.Topic.CreatedAt,
                bookmark.CreatedAt))
            .ToListAsync(cancellationToken);

        return BookmarkListResult.Succeeded(new BookmarkListResponse(items, page, pageSize, totalCount));
    }

    private async Task<BookmarkFailure> GetAccessFailureAsync(long userId, CancellationToken cancellationToken)
    {
        if (await banService.IsBannedAsync(userId, cancellationToken))
        {
            return BookmarkFailure.Banned;
        }

        return await dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken)
            ? BookmarkFailure.None
            : BookmarkFailure.UserNotFound;
    }

    private bool TryGetPage(BookmarkPageRequest request, out int page, out int pageSize, out int skip)
    {
        page = request.Page;
        pageSize = Math.Min(request.PageSize ?? paginationOptions.DefaultPageSize, paginationOptions.MaxPageSize);
        skip = 0;
        if (page < 1 || pageSize < 1)
        {
            return false;
        }

        var skipValue = ((long)page - 1) * pageSize;
        if (skipValue > int.MaxValue)
        {
            return false;
        }

        skip = (int)skipValue;
        return true;
    }
}
