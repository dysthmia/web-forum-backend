namespace Forum.Api.Infrastructure.Persistence.Entities;

public class UserBan
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long ModeratorId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public User User { get; set; } = null!;

    public User Moderator { get; set; } = null!;
}
