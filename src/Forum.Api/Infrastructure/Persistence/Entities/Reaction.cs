namespace Forum.Api.Infrastructure.Persistence.Entities;

public enum ReactionType
{
    Dislike = -1,
    Like = 1
}

public class Reaction
{
    public long UserId { get; set; }

    public long PostId { get; set; }

    public ReactionType Type { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;

    public Post Post { get; set; } = null!;
}
