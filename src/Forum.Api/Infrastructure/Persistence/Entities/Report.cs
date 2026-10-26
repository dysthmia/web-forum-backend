namespace Forum.Api.Infrastructure.Persistence.Entities;

public enum ReportStatus
{
    Pending,
    Accepted,
    Rejected
}

public class Report
{
    public long Id { get; set; }

    public long ReporterId { get; set; }

    public long PostId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public ReportStatus Status { get; set; }

    public long? ResolvedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public User Reporter { get; set; } = null!;

    public Post Post { get; set; } = null!;

    public User? ResolvedByUser { get; set; }
}
