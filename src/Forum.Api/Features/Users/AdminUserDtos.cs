using System.ComponentModel.DataAnnotations;

namespace Forum.Api.Features.Users;

public class AdminUserListRequest
{
    [StringLength(32)]
    public string? Query { get; init; }

    public string? Role { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, int.MaxValue)]
    public int? PageSize { get; init; }
}

public class ChangeUserRoleRequest
{
    [Required]
    public string? Role { get; init; }
}

public record AdminUserResponse(long Id, string Username, string Role, DateTime CreatedAt, bool IsBanned);

public record AdminUserListResponse(IReadOnlyList<AdminUserResponse> Items, int Page, int PageSize, int TotalCount);
