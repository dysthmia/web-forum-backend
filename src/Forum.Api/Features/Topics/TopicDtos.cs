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
