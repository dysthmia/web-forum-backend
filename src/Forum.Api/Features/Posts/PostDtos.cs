using System.ComponentModel.DataAnnotations;

namespace Forum.Api.Features.Posts;

public class CreatePostRequest
{
    [Required]
    public string? Content { get; init; }
}

public record CreatePostResponse(long PostId, int Position);
