using System.ComponentModel.DataAnnotations;

namespace Forum.Api.Features.Topics;

public class CreateTopicRequest
{
    [Range(1, long.MaxValue)]
    public long CategoryId { get; init; }

    [Required]
    [StringLength(200)]
    public string? Title { get; init; }

    [Required]
    public string? Content { get; init; }
}

public record CreateTopicResponse(long TopicId);

public class UpdateTopicRequest
{
    [Required]
    [StringLength(200)]
    public string? Title { get; init; }
}

public class SetTopicClosedRequest
{
    public bool IsClosed { get; init; }
}

public class SetTopicPinnedRequest
{
    public bool IsPinned { get; init; }
}

public class TopicPageRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, int.MaxValue)]
    public int? PageSize { get; init; }
}

public class GetCategoryTopicsRequest : TopicPageRequest
{
    public string? Sort { get; init; }
}

public class GetTopicsRequest : TopicPageRequest
{
    public string? Sort { get; init; }
}

public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public record TopicListItemResponse(
    long Id,
    long CategoryId,
    string CategoryName,
    long AuthorId,
    string AuthorUsername,
    string Title,
    long ViewCount,
    bool IsClosed,
    bool IsPinned,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int ReplyCount);

public record TopicPostResponse(
    long Id,
    long AuthorId,
    string AuthorUsername,
    int Position,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record TopicDetailsResponse(
    long Id,
    long CategoryId,
    long AuthorId,
    string Title,
    long ViewCount,
    bool IsClosed,
    bool IsPinned,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    TopicPostResponse FirstPost,
    PagedResponse<TopicPostResponse> Replies);

public class TopicPaginationOptions
{
    public required int DefaultPageSize { get; init; }

    public required int MaxPageSize { get; init; }
}

public static class TopicPaginationOptionsFactory
{
    private const int AbsoluteMaxPageSize = 100;

    public static TopicPaginationOptions Create(IConfiguration configuration)
    {
        var defaultPageSize = configuration.GetValue<int?>("TOPICS_DEFAULT_PAGE_SIZE")
            ?? throw new InvalidOperationException("TOPICS_DEFAULT_PAGE_SIZE is required.");
        var maxPageSize = configuration.GetValue<int?>("TOPICS_MAX_PAGE_SIZE")
            ?? throw new InvalidOperationException("TOPICS_MAX_PAGE_SIZE is required.");

        if (defaultPageSize < 1 || maxPageSize < defaultPageSize || maxPageSize > AbsoluteMaxPageSize)
        {
            throw new InvalidOperationException(
                $"Topics page sizes must be positive, ordered, and no greater than {AbsoluteMaxPageSize}.");
        }

        return new TopicPaginationOptions
        {
            DefaultPageSize = defaultPageSize,
            MaxPageSize = maxPageSize
        };
    }
}
