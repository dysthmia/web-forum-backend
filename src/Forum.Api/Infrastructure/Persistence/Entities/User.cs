namespace Forum.Api.Infrastructure.Persistence.Entities;

public enum UserRole
{
    User,
    Moderator,
    Admin
}

public class User
{
    public long Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public DateTime CreatedAt { get; set; }
}
