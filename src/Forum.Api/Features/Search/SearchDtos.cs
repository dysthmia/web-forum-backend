using System.ComponentModel.DataAnnotations;

namespace Forum.Api.Features.Search;

public class SearchRequest
{
    [Required]
    [StringLength(200)]
    public string? Query { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, int.MaxValue)]
    public int? PageSize { get; init; }
}

public record SearchTopicResponse(
    long TopicId,
    long CategoryId,
    string Title,
    string Fragment,
    float Relevance,
    DateTime CreatedAt);

public record SearchResponse(IReadOnlyList<SearchTopicResponse> Items, int Page, int PageSize, int TotalCount);
