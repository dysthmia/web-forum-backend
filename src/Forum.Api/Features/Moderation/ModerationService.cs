using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Forum.Api.Features.Moderation;

public enum ReviewReportFailure
{
    None,
    InvalidRequest,
    ReviewerNotFound,
    ReportNotFound,
    AlreadyResolved,
    FirstPostCannotBeHidden
}

public record BanActor(long UserId, bool IsAdmin);

public enum BanFailure
{
    None,
    InvalidRequest,
    UserNotFound,
    Forbidden,
    ActiveBanExists,
    BanNotFound,
    AlreadyRevoked,
    BanIsNotActive
}

public record BanResult(BanResponse? Response, BanFailure Failure)
{
    public static BanResult Succeeded(BanResponse response) => new(response, BanFailure.None);

    public static BanResult Failed(BanFailure failure) => new(null, failure);
}

public class ModerationService(ForumDbContext dbContext)
{
    public async Task<IReadOnlyList<PendingReportResponse>> GetPendingReportsAsync(CancellationToken cancellationToken) =>
        await dbContext.Reports.AsNoTracking()
            .Where(report => report.Status == ReportStatus.Pending)
            .OrderBy(report => report.CreatedAt)
            .Select(report => new PendingReportResponse(
                report.Id,
                report.PostId,
                report.ReporterId,
                report.Reporter.Username,
                report.Reason,
                report.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<ReviewReportFailure> ReviewAsync(
        long reportId,
        long reviewerId,
        ReviewReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetStatus(request.Status, out var status))
        {
            return ReviewReportFailure.InvalidRequest;
        }

        if (!await dbContext.Users.AnyAsync(user => user.Id == reviewerId, cancellationToken))
        {
            return ReviewReportFailure.ReviewerNotFound;
        }

        var report = await dbContext.Reports.AsNoTracking()
            .Where(item => item.Id == reportId)
            .Select(item => new { item.Status, item.PostId, PostPosition = item.Post.Position })
            .SingleOrDefaultAsync(cancellationToken);
        if (report is null)
        {
            return ReviewReportFailure.ReportNotFound;
        }

        if (report.Status != ReportStatus.Pending)
        {
            return report.Status == status
                ? ReviewReportFailure.None
                : ReviewReportFailure.AlreadyResolved;
        }

        if (request.HidePost && status != ReportStatus.Accepted)
        {
            return ReviewReportFailure.InvalidRequest;
        }

        if (request.HidePost && report.PostPosition == 1)
        {
            return ReviewReportFailure.FirstPostCannotBeHidden;
        }

        if (!dbContext.Database.IsRelational())
        {
            return await ReviewInMemoryAsync(reportId, reviewerId, status, request.HidePost, cancellationToken);
        }

        try
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"CALL review_report({reportId}, {reviewerId}, {status.ToString().ToLowerInvariant()}, {request.HidePost})",
                cancellationToken);
            return ReviewReportFailure.None;
        }
        catch (PostgresException)
        {
            return await GetFailureAfterConcurrentReviewAsync(reportId, status, cancellationToken);
        }
    }

    private async Task<ReviewReportFailure> ReviewInMemoryAsync(
        long reportId,
        long reviewerId,
        ReportStatus status,
        bool hidePost,
        CancellationToken cancellationToken)
    {
        var report = await dbContext.Reports.SingleAsync(item => item.Id == reportId, cancellationToken);
        if (report.Status != ReportStatus.Pending)
        {
            return report.Status == status ? ReviewReportFailure.None : ReviewReportFailure.AlreadyResolved;
        }

        if (hidePost)
        {
            var post = await dbContext.Posts.SingleAsync(item => item.Id == report.PostId, cancellationToken);
            post.DeletedAt = DateTime.UtcNow;
            post.DeletedBy = reviewerId;
        }

        report.Status = status;
        report.ResolvedBy = reviewerId;
        report.ResolvedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ReviewReportFailure.None;
    }

    private async Task<ReviewReportFailure> GetFailureAfterConcurrentReviewAsync(
        long reportId,
        ReportStatus requestedStatus,
        CancellationToken cancellationToken)
    {
        var currentStatus = await dbContext.Reports.AsNoTracking()
            .Where(report => report.Id == reportId)
            .Select(report => (ReportStatus?)report.Status)
            .SingleOrDefaultAsync(cancellationToken);
        if (currentStatus is null)
        {
            return ReviewReportFailure.ReportNotFound;
        }

        return currentStatus == requestedStatus
            ? ReviewReportFailure.None
            : ReviewReportFailure.AlreadyResolved;
    }

    private static bool TryGetStatus(string? value, out ReportStatus status)
    {
        status = default;
        if (string.Equals(value, "accepted", StringComparison.OrdinalIgnoreCase))
        {
            status = ReportStatus.Accepted;
            return true;
        }

        if (string.Equals(value, "rejected", StringComparison.OrdinalIgnoreCase))
        {
            status = ReportStatus.Rejected;
            return true;
        }

        return false;
    }

    public async Task<BanResult> CreateBanAsync(
        BanActor actor,
        CreateBanRequest request,
        CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim() ?? string.Empty;
        var now = DateTime.UtcNow;
        if (request.UserId < 1 || reason.Length == 0 || (request.ExpiresAt is not null && request.ExpiresAt <= now))
        {
            return BanResult.Failed(BanFailure.InvalidRequest);
        }

        if (!actor.IsAdmin && request.ExpiresAt is null)
        {
            return BanResult.Failed(BanFailure.Forbidden);
        }

        if (!dbContext.Database.IsRelational())
        {
            return await CreateBanWithLockedUserAsync(actor, request, reason, now, false, cancellationToken);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await CreateBanWithLockedUserAsync(actor, request, reason, now, true, cancellationToken);
        if (result.Failure == BanFailure.None)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }

    public async Task<BanFailure> RevokeBanAsync(long banId, BanActor actor, CancellationToken cancellationToken)
    {
        var userId = await dbContext.UserBans.AsNoTracking()
            .Where(ban => ban.Id == banId)
            .Select(ban => (long?)ban.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (userId is null)
        {
            return BanFailure.BanNotFound;
        }

        if (!dbContext.Database.IsRelational())
        {
            return await RevokeBanWithLockedUserAsync(banId, userId.Value, actor, false, cancellationToken);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var failure = await RevokeBanWithLockedUserAsync(banId, userId.Value, actor, true, cancellationToken);
        if (failure == BanFailure.None)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return failure;
    }

    public async Task<(IReadOnlyList<BanHistoryResponse>? History, BanFailure Failure)> GetBanHistoryAsync(
        long userId,
        BanActor actor,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return (null, BanFailure.UserNotFound);
        }

        if (!CanManageUser(actor, user))
        {
            return (null, BanFailure.Forbidden);
        }

        var history = await dbContext.UserBans.AsNoTracking()
            .Where(ban => ban.UserId == userId)
            .OrderByDescending(ban => ban.CreatedAt)
            .Select(ban => new BanHistoryResponse(
                ban.Id,
                ban.UserId,
                ban.User.Username,
                ban.ModeratorId,
                ban.Moderator.Username,
                ban.Reason,
                ban.CreatedAt,
                ban.ExpiresAt,
                ban.RevokedAt))
            .ToListAsync(cancellationToken);
        return (history, BanFailure.None);
    }

    private async Task<BanResult> CreateBanWithLockedUserAsync(
        BanActor actor,
        CreateBanRequest request,
        string reason,
        DateTime now,
        bool lockUser,
        CancellationToken cancellationToken)
    {
        var user = lockUser
            ? await dbContext.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {request.UserId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
            : await dbContext.Users.SingleOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return BanResult.Failed(BanFailure.UserNotFound);
        }

        if (actor.UserId == user.Id || !CanManageUser(actor, user))
        {
            return BanResult.Failed(BanFailure.Forbidden);
        }

        var activeBanExists = await dbContext.UserBans.AnyAsync(
            ban => ban.UserId == user.Id
                   && ban.RevokedAt == null
                   && (ban.ExpiresAt == null || ban.ExpiresAt > now),
            cancellationToken);
        if (activeBanExists)
        {
            return BanResult.Failed(BanFailure.ActiveBanExists);
        }

        var ban = new UserBan
        {
            UserId = user.Id,
            ModeratorId = actor.UserId,
            Reason = reason,
            CreatedAt = now,
            ExpiresAt = request.ExpiresAt
        };
        dbContext.UserBans.Add(ban);
        await dbContext.SaveChangesAsync(cancellationToken);
        return BanResult.Succeeded(new BanResponse(ban.Id, ban.UserId, ban.ExpiresAt));
    }

    private async Task<BanFailure> RevokeBanWithLockedUserAsync(
        long banId,
        long userId,
        BanActor actor,
        bool lockUser,
        CancellationToken cancellationToken)
    {
        var user = lockUser
            ? await dbContext.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
            : await dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return BanFailure.UserNotFound;
        }

        if (!CanManageUser(actor, user))
        {
            return BanFailure.Forbidden;
        }

        var ban = await dbContext.UserBans.SingleOrDefaultAsync(item => item.Id == banId, cancellationToken);
        if (ban is null)
        {
            return BanFailure.BanNotFound;
        }

        if (ban.RevokedAt is not null)
        {
            return BanFailure.AlreadyRevoked;
        }

        if (ban.ExpiresAt is not null && ban.ExpiresAt <= DateTime.UtcNow)
        {
            return BanFailure.BanIsNotActive;
        }

        ban.RevokedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return BanFailure.None;
    }

    private static bool CanManageUser(BanActor actor, User user) =>
        user.Role != UserRole.Admin && (actor.IsAdmin || user.Role == UserRole.User);
}
