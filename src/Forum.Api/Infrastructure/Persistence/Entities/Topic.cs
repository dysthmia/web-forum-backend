namespace Forum.Api.Infrastructure.Persistence.Entities;

public class Topic
{
    public long Id { get; set; }

    public long CategoryId { get; set; }

    public long AuthorId { get; set; }

    public string Title { get; set; } = string.Empty;

    public long ViewCount { get; set; }

    public bool IsClosed { get; set; }

    public bool IsPinned { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public Category Category { get; set; } = null!;

    public User Author { get; set; } = null!;

    public User? DeletedByUser { get; set; }
}
