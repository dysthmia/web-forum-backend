using System.ComponentModel.DataAnnotations;

namespace Forum.Api.Features.Moderation;

public class CreateBanRequest
{
    [Range(1, long.MaxValue)]
    public long UserId { get; init; }

    [Required]
    public string? Reason { get; init; }

    public DateTime? ExpiresAt { get; init; }
}

public record BanResponse(long BanId, long UserId, DateTime? ExpiresAt);

public record BanHistoryResponse(
    long BanId,
    long UserId,
    string Username,
    long ModeratorId,
    string ModeratorUsername,
    string Reason,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    DateTime? RevokedAt);
