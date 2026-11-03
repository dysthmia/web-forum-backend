using Forum.Api.Common.Authorization;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Forum.Api.Features.Reports;

public enum CreateReportFailure
{
    None,
    InvalidRequest,
    ReporterNotFound,
    Banned,
    PostNotFound,
    DuplicateReport
}

public record CreateReportResult(CreateReportResponse? Response, CreateReportFailure Failure)
{
    public static CreateReportResult Succeeded(CreateReportResponse response) => new(response, CreateReportFailure.None);

    public static CreateReportResult Failed(CreateReportFailure failure) => new(null, failure);
}

public class ReportsService(ForumDbContext dbContext, ActiveUserBanService banService)
{
    public async Task<CreateReportResult> CreateAsync(
        long reporterId,
        CreateReportRequest request,
        CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (request.PostId < 1 || reason.Length == 0)
        {
            return CreateReportResult.Failed(CreateReportFailure.InvalidRequest);
        }

        if (await banService.IsBannedAsync(reporterId, cancellationToken))
        {
            return CreateReportResult.Failed(CreateReportFailure.Banned);
        }

        if (!await dbContext.Users.AnyAsync(user => user.Id == reporterId, cancellationToken))
        {
            return CreateReportResult.Failed(CreateReportFailure.ReporterNotFound);
        }

        if (!await dbContext.Posts.AsNoTracking().AnyAsync(
                post => post.Id == request.PostId
                        && post.DeletedAt == null
                        && post.Topic.DeletedAt == null,
                cancellationToken))
        {
            return CreateReportResult.Failed(CreateReportFailure.PostNotFound);
        }

        if (await dbContext.Reports.AnyAsync(
                report => report.ReporterId == reporterId && report.PostId == request.PostId,
                cancellationToken))
        {
            return CreateReportResult.Failed(CreateReportFailure.DuplicateReport);
        }

        var report = new Report
        {
            ReporterId = reporterId,
            PostId = request.PostId,
            Reason = reason,
            Status = ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ResolvedBy = null,
            ResolvedAt = null
        };
        dbContext.Reports.Add(report);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return CreateReportResult.Succeeded(new CreateReportResponse(report.Id, "pending"));
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return CreateReportResult.Failed(CreateReportFailure.DuplicateReport);
        }
    }
}
