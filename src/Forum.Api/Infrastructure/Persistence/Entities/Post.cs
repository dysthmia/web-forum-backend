namespace Forum.Api.Infrastructure.Persistence.Entities;

public class Post
{
    public long Id { get; set; }

    public long TopicId { get; set; }

    public long AuthorId { get; set; }

    public int Position { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public Topic Topic { get; set; } = null!;

    public User Author { get; set; } = null!;

    public User? DeletedByUser { get; set; }
}
