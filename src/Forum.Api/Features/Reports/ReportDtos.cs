using System.ComponentModel.DataAnnotations;

namespace Forum.Api.Features.Reports;

public class CreateReportRequest
{
    [Range(1, long.MaxValue)]
    public long PostId { get; init; }

    [Required]
    public string? Reason { get; init; }
}

public record CreateReportResponse(long ReportId, string Status);
