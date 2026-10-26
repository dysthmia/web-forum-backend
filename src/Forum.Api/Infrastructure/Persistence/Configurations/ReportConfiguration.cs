using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Forum.Api.Infrastructure.Persistence.Configurations;

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("reports", table =>
        {
            table.HasCheckConstraint("ck_reports_status", "status IN ('pending', 'accepted', 'rejected')");
            table.HasCheckConstraint(
                "ck_reports_resolution",
                "(status = 'pending' AND resolved_by IS NULL AND resolved_at IS NULL) OR (status IN ('accepted', 'rejected') AND resolved_by IS NOT NULL AND resolved_at IS NOT NULL)");
        });

        builder.HasKey(report => report.Id);
        builder.Property(report => report.Id).UseIdentityByDefaultColumn();
        builder.Property(report => report.ReporterId).HasColumnName("reporter_id").IsRequired();
        builder.Property(report => report.PostId).HasColumnName("post_id").IsRequired();
        builder.Property(report => report.Reason).HasColumnName("reason").IsRequired();
        builder.Property(report => report.Status)
            .HasColumnName("status")
            .HasConversion(
                status => status.ToString().ToLowerInvariant(),
                value => Enum.Parse<ReportStatus>(value, true))
            .HasColumnType("varchar(16)")
            .HasDefaultValue(ReportStatus.Pending)
            .IsRequired();
        builder.Property(report => report.ResolvedBy).HasColumnName("resolved_by");
        builder.Property(report => report.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();
        builder.Property(report => report.ResolvedAt).HasColumnName("resolved_at").HasColumnType("timestamp with time zone");

        builder.HasIndex(report => new { report.ReporterId, report.PostId }).IsUnique();
        builder.HasIndex(report => report.CreatedAt)
            .HasFilter("status = 'pending'")
            .HasDatabaseName("ix_reports_pending_queue");

        builder.HasOne(report => report.Reporter).WithMany().HasForeignKey(report => report.ReporterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(report => report.Post).WithMany().HasForeignKey(report => report.PostId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(report => report.ResolvedByUser).WithMany().HasForeignKey(report => report.ResolvedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
