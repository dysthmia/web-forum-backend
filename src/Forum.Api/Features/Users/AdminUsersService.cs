using Forum.Api.Features.Topics;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Forum.Api.Features.Users;

public enum AdminUserFailure
{
    None,
    InvalidRequest,
    NotFound,
    Forbidden
}

public record AdminUserResult(AdminUserResponse? Response, AdminUserFailure Failure)
{
    public static AdminUserResult Succeeded(AdminUserResponse response) => new(response, AdminUserFailure.None);

    public static AdminUserResult Failed(AdminUserFailure failure) => new(null, failure);
}

public class AdminUsersService(ForumDbContext dbContext, TopicPaginationOptions paginationOptions)
{
    public async Task<(AdminUserListResponse? Response, AdminUserFailure Failure)> GetListAsync(
        AdminUserListRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetPage(request, out var page, out var pageSize, out var skip)
            || !TryParseRole(request.Role, out var role))
        {
            return (null, AdminUserFailure.InvalidRequest);
        }

        var queryText = request.Query?.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;
        var users = dbContext.Users.AsNoTracking()
            .Where(user => string.IsNullOrEmpty(queryText) || user.Username.ToLower().Contains(queryText));
        if (role.HasValue)
        {
            users = users.Where(user => user.Role == role.Value);
        }
        var totalCount = await users.CountAsync(cancellationToken);
        var items = await users.OrderBy(user => user.Username)
            .Skip(skip)
            .Take(pageSize)
            .Select(user => new AdminUserResponse(
                user.Id,
                user.Username,
                user.Role.ToString().ToLowerInvariant(),
                user.CreatedAt,
                dbContext.UserBans.Any(ban => ban.UserId == user.Id
                                              && ban.RevokedAt == null
                                              && (ban.ExpiresAt == null || ban.ExpiresAt > now))))
            .ToListAsync(cancellationToken);
        return (new AdminUserListResponse(items, page, pageSize, totalCount), AdminUserFailure.None);
    }

    public async Task<AdminUserResult> ChangeRoleAsync(
        long actorId,
        long userId,
        ChangeUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (actorId == userId)
        {
            return AdminUserResult.Failed(AdminUserFailure.Forbidden);
        }

        if (!TryParseRole(request.Role, out var role) || role is null)
        {
            return AdminUserResult.Failed(AdminUserFailure.InvalidRequest);
        }

        if (role == UserRole.Admin)
        {
            return AdminUserResult.Failed(AdminUserFailure.Forbidden);
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return AdminUserResult.Failed(AdminUserFailure.NotFound);
        }

        if (user.Role == UserRole.Admin)
        {
            return AdminUserResult.Failed(AdminUserFailure.Forbidden);
        }

        user.Role = role.Value;
        await dbContext.SaveChangesAsync(cancellationToken);
        var isBanned = await IsBannedAsync(userId, DateTime.UtcNow, cancellationToken);
        return AdminUserResult.Succeeded(ToResponse(user, isBanned));
    }

    private bool TryGetPage(AdminUserListRequest request, out int page, out int pageSize, out int skip)
    {
        page = request.Page;
        pageSize = Math.Min(request.PageSize ?? paginationOptions.DefaultPageSize, paginationOptions.MaxPageSize);
        skip = 0;
        if (page < 1 || pageSize < 1)
        {
            return false;
        }

        var skipValue = ((long)page - 1) * pageSize;
        if (skipValue > int.MaxValue)
        {
            return false;
        }

        skip = (int)skipValue;
        return true;
    }

    private static bool TryParseRole(string? rawRole, out UserRole? role)
    {
        role = null;
        if (string.IsNullOrWhiteSpace(rawRole))
        {
            return true;
        }

        if (!Enum.TryParse<UserRole>(rawRole, true, out var parsedRole) || !Enum.IsDefined(parsedRole))
        {
            return false;
        }

        role = parsedRole;
        return true;
    }

    private Task<bool> IsBannedAsync(long userId, DateTime now, CancellationToken cancellationToken) =>
        dbContext.UserBans.AsNoTracking().AnyAsync(ban => ban.UserId == userId
                                                       && ban.RevokedAt == null
                                                       && (ban.ExpiresAt == null || ban.ExpiresAt > now), cancellationToken);

    private static AdminUserResponse ToResponse(User user, bool isBanned) =>
        new(user.Id, user.Username, user.Role.ToString().ToLowerInvariant(), user.CreatedAt, isBanned);
}
