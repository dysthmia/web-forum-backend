namespace Forum.Api.Infrastructure.Persistence.Entities;

public class RefreshToken
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public User User { get; set; } = null!;
}
