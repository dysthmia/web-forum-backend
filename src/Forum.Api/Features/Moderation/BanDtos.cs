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

public class BanListRequest
{
    [StringLength(32)]
    public string? Query { get; init; }

    public string? Status { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, int.MaxValue)]
    public int? PageSize { get; init; }
}

public record BanListResponse(IReadOnlyList<BanHistoryResponse> Items, int Page, int PageSize, int TotalCount);
