using Forum.Api.Common.Authorization;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Forum.Api.Features.Topics;

public enum CreateTopicFailure
{
    None,
    InvalidRequest,
    CategoryNotFound,
    AuthorNotFound,
    Banned,
    CreationFailed
}

public record CreateTopicResult(CreateTopicResponse? Response, CreateTopicFailure Failure)
{
    public static CreateTopicResult Succeeded(CreateTopicResponse response) => new(response, CreateTopicFailure.None);

    public static CreateTopicResult Failed(CreateTopicFailure failure) => new(null, failure);
}

public enum TopicReadFailure
{
    None,
    InvalidRequest,
    CategoryNotFound,
    TopicNotFound
}

public record TopicListResult(PagedResponse<TopicListItemResponse>? Response, TopicReadFailure Failure)
{
    public static TopicListResult Succeeded(PagedResponse<TopicListItemResponse> response) => new(response, TopicReadFailure.None);

    public static TopicListResult Failed(TopicReadFailure failure) => new(null, failure);
}

public record TopicDetailsResult(TopicDetailsResponse? Response, TopicReadFailure Failure)
{
    public static TopicDetailsResult Succeeded(TopicDetailsResponse response) => new(response, TopicReadFailure.None);

    public static TopicDetailsResult Failed(TopicReadFailure failure) => new(null, failure);
}

public record TopicActor(long UserId, bool CanModerate);

public enum TopicManagementFailure
{
    None,
    InvalidRequest,
    NotFound,
    Forbidden
}

public class TopicsService(
    ForumDbContext dbContext,
    ActiveUserBanService banService,
    TopicPaginationOptions paginationOptions)
{
    public async Task<CreateTopicResult> CreateAsync(
        long authorId,
        CreateTopicRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryPrepare(request, out var title, out var content))
        {
            return CreateTopicResult.Failed(CreateTopicFailure.InvalidRequest);
        }

        if (await banService.IsBannedAsync(authorId, cancellationToken))
        {
            return CreateTopicResult.Failed(CreateTopicFailure.Banned);
        }

        if (!await dbContext.Categories.AnyAsync(category => category.Id == request.CategoryId, cancellationToken))
        {
            return CreateTopicResult.Failed(CreateTopicFailure.CategoryNotFound);
        }

        if (!await dbContext.Users.AnyAsync(user => user.Id == authorId, cancellationToken))
        {
            return CreateTopicResult.Failed(CreateTopicFailure.AuthorNotFound);
        }

        IDbContextTransaction? transaction = null;
        if (dbContext.Database.IsRelational())
        {
            transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        await using (transaction)
        {
            try
            {
                var now = DateTime.UtcNow;
                var topic = new Topic
                {
                    CategoryId = request.CategoryId,
                    AuthorId = authorId,
                    Title = title,
                    ViewCount = 0,
                    IsClosed = false,
                    IsPinned = false,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                dbContext.Topics.Add(topic);
                await dbContext.SaveChangesAsync(cancellationToken);

                dbContext.Posts.Add(new Post
                {
                    TopicId = topic.Id,
                    AuthorId = authorId,
                    Position = 1,
                    Content = content,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                await dbContext.SaveChangesAsync(cancellationToken);

                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return CreateTopicResult.Succeeded(new CreateTopicResponse(topic.Id));
            }
            catch (DbUpdateException)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }

                return CreateTopicResult.Failed(CreateTopicFailure.CreationFailed);
            }
        }
    }

    private static bool TryPrepare(CreateTopicRequest request, out string title, out string content)
    {
        title = request.Title?.Trim() ?? string.Empty;
        content = request.Content?.Trim() ?? string.Empty;

        return request.CategoryId > 0 && title.Length is > 0 and <= 200 && content.Length > 0;
    }

    public async Task<TopicListResult> GetCategoryTopicsAsync(
        long categoryId,
        GetCategoryTopicsRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetPage(request, out var page, out var pageSize, out var skip)
            || !TryGetSort(request.Sort, out var sortByPopularity))
        {
            return TopicListResult.Failed(TopicReadFailure.InvalidRequest);
        }

        if (!await dbContext.Categories.AsNoTracking().AnyAsync(category => category.Id == categoryId, cancellationToken))
        {
            return TopicListResult.Failed(TopicReadFailure.CategoryNotFound);
        }

        var query = dbContext.Topics.AsNoTracking()
            .Where(topic => topic.CategoryId == categoryId && topic.DeletedAt == null);
        var totalCount = await query.CountAsync(cancellationToken);
        var orderedQuery = sortByPopularity
            ? query.OrderByDescending(topic => topic.IsPinned)
                .ThenByDescending(topic => topic.ViewCount)
                .ThenByDescending(topic => topic.CreatedAt)
            : query.OrderByDescending(topic => topic.IsPinned)
                .ThenByDescending(topic => topic.CreatedAt);

        var items = await orderedQuery
            .Skip(skip)
            .Take(pageSize)
            .Select(topic => new TopicListItemResponse(
                topic.Id,
                topic.AuthorId,
                topic.Title,
                topic.ViewCount,
                topic.IsClosed,
                topic.IsPinned,
                topic.CreatedAt,
                topic.UpdatedAt,
                dbContext.Posts.Count(post => post.TopicId == topic.Id
                                               && post.Position > 1
                                               && post.DeletedAt == null)))
            .ToListAsync(cancellationToken);

        return TopicListResult.Succeeded(new PagedResponse<TopicListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<TopicDetailsResult> GetDetailsAsync(
        long topicId,
        TopicPageRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetPage(request, out var page, out var pageSize, out var skip))
        {
            return TopicDetailsResult.Failed(TopicReadFailure.InvalidRequest);
        }

        var topic = await dbContext.Topics.AsNoTracking()
            .Where(item => item.Id == topicId && item.DeletedAt == null)
            .Select(item => new
            {
                item.Id,
                item.CategoryId,
                item.AuthorId,
                item.Title,
                item.ViewCount,
                item.IsClosed,
                item.IsPinned,
                item.CreatedAt,
                item.UpdatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (topic is null)
        {
            return TopicDetailsResult.Failed(TopicReadFailure.TopicNotFound);
        }

        var firstPost = await dbContext.Posts.AsNoTracking()
            .Where(post => post.TopicId == topicId && post.Position == 1 && post.DeletedAt == null)
            .Select(post => new TopicPostResponse(
                post.Id,
                post.AuthorId,
                post.Position,
                post.Content,
                post.CreatedAt,
                post.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
        if (firstPost is null)
        {
            return TopicDetailsResult.Failed(TopicReadFailure.TopicNotFound);
        }

        var replies = dbContext.Posts.AsNoTracking()
            .Where(post => post.TopicId == topicId && post.Position > 1 && post.DeletedAt == null);
        var totalCount = await replies.CountAsync(cancellationToken);
        var items = await replies.OrderBy(post => post.Position)
            .Skip(skip)
            .Take(pageSize)
            .Select(post => new TopicPostResponse(
                post.Id,
                post.AuthorId,
                post.Position,
                post.Content,
                post.CreatedAt,
                post.UpdatedAt))
            .ToListAsync(cancellationToken);

        return TopicDetailsResult.Succeeded(new TopicDetailsResponse(
            topic.Id,
            topic.CategoryId,
            topic.AuthorId,
            topic.Title,
            topic.ViewCount,
            topic.IsClosed,
            topic.IsPinned,
            topic.CreatedAt,
            topic.UpdatedAt,
            firstPost,
            new PagedResponse<TopicPostResponse>(items, page, pageSize, totalCount)));
    }

    private bool TryGetPage(TopicPageRequest request, out int page, out int pageSize, out int skip)
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

    private static bool TryGetSort(string? sort, out bool sortByPopularity)
    {
        sortByPopularity = string.Equals(sort, "popular", StringComparison.OrdinalIgnoreCase);
        return string.IsNullOrWhiteSpace(sort)
               || string.Equals(sort, "recent", StringComparison.OrdinalIgnoreCase)
               || sortByPopularity;
    }

    public async Task<TopicManagementFailure> UpdateTitleAsync(
        long topicId,
        TopicActor actor,
        UpdateTopicRequest request,
        CancellationToken cancellationToken)
    {
        var title = request.Title?.Trim() ?? string.Empty;
        if (title.Length is 0 or > 200)
        {
            return TopicManagementFailure.InvalidRequest;
        }

        var topic = await GetActiveTopicAsync(topicId, cancellationToken);
        if (topic is null)
        {
            return TopicManagementFailure.NotFound;
        }

        if (!CanManage(topic, actor))
        {
            return TopicManagementFailure.Forbidden;
        }

        topic.Title = title;
        topic.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return TopicManagementFailure.None;
    }

    public async Task<TopicManagementFailure> SetClosedAsync(
        long topicId,
        TopicActor actor,
        SetTopicClosedRequest request,
        CancellationToken cancellationToken)
    {
        if (!actor.CanModerate)
        {
            return TopicManagementFailure.Forbidden;
        }

        var topic = await GetActiveTopicAsync(topicId, cancellationToken);
        if (topic is null)
        {
            return TopicManagementFailure.NotFound;
        }

        topic.IsClosed = request.IsClosed;
        topic.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return TopicManagementFailure.None;
    }

    public async Task<TopicManagementFailure> SetPinnedAsync(
        long topicId,
        TopicActor actor,
        SetTopicPinnedRequest request,
        CancellationToken cancellationToken)
    {
        if (!actor.CanModerate)
        {
            return TopicManagementFailure.Forbidden;
        }

        var topic = await GetActiveTopicAsync(topicId, cancellationToken);
        if (topic is null)
        {
            return TopicManagementFailure.NotFound;
        }

        topic.IsPinned = request.IsPinned;
        topic.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return TopicManagementFailure.None;
    }

    public async Task<TopicManagementFailure> DeleteAsync(
        long topicId,
        TopicActor actor,
        CancellationToken cancellationToken)
    {
        var topic = await GetActiveTopicAsync(topicId, cancellationToken);
        if (topic is null)
        {
            return TopicManagementFailure.NotFound;
        }

        if (!CanManage(topic, actor))
        {
            return TopicManagementFailure.Forbidden;
        }

        var now = DateTime.UtcNow;
        topic.DeletedAt = now;
        topic.DeletedBy = actor.UserId;
        topic.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return TopicManagementFailure.None;
    }

    private Task<Topic?> GetActiveTopicAsync(long topicId, CancellationToken cancellationToken) =>
        dbContext.Topics.SingleOrDefaultAsync(topic => topic.Id == topicId && topic.DeletedAt == null, cancellationToken);

    private static bool CanManage(Topic topic, TopicActor actor) =>
        actor.CanModerate || topic.AuthorId == actor.UserId;
}
