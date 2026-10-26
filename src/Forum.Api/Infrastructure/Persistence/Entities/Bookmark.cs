namespace Forum.Api.Infrastructure.Persistence.Entities;

public class Bookmark
{
    public long UserId { get; set; }

    public long TopicId { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;

    public Topic Topic { get; set; } = null!;
}
