using System.ComponentModel.DataAnnotations;

namespace Forum.Api.Features.Bookmarks;

public class BookmarkPageRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, int.MaxValue)]
    public int? PageSize { get; init; }
}

public record BookmarkTopicResponse(
    long TopicId,
    string Title,
    bool IsClosed,
    bool IsPinned,
    DateTime TopicCreatedAt,
    DateTime BookmarkedAt);

public record BookmarkListResponse(
    IReadOnlyList<BookmarkTopicResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
