using System.ComponentModel.DataAnnotations;

namespace Forum.Api.Features.Moderation;

public class ReviewReportRequest
{
    [Required]
    public string? Status { get; init; }

    public bool HidePost { get; init; }
}

public record PendingReportResponse(
    long ReportId,
    long PostId,
    long ReporterId,
    string ReporterUsername,
    string Reason,
    DateTime CreatedAt);
