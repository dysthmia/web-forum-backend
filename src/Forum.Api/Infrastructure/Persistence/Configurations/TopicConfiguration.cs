using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Forum.Api.Infrastructure.Persistence.Configurations;

public class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.ToTable("topics", table => table.HasCheckConstraint("ck_topics_view_count", "view_count >= 0"));

        builder.HasKey(topic => topic.Id);
        builder.Property(topic => topic.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        builder.Property(topic => topic.CategoryId).HasColumnName("category_id").IsRequired();
        builder.Property(topic => topic.AuthorId).HasColumnName("author_id").IsRequired();
        builder.Property(topic => topic.Title).HasColumnName("title").HasColumnType("varchar(200)").IsRequired();
        builder.Property(topic => topic.ViewCount).HasColumnName("view_count").HasDefaultValue(0L).IsRequired();
        builder.Property(topic => topic.IsClosed).HasColumnName("is_closed").HasDefaultValue(false).IsRequired();
        builder.Property(topic => topic.IsPinned).HasColumnName("is_pinned").HasDefaultValue(false).IsRequired();
        builder.Property(topic => topic.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();
        builder.Property(topic => topic.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();
        builder.Property(topic => topic.DeletedAt).HasColumnName("deleted_at").HasColumnType("timestamp with time zone");
        builder.Property(topic => topic.DeletedBy).HasColumnName("deleted_by");

        builder.HasIndex(topic => new { topic.CategoryId, topic.CreatedAt })
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ix_topics_active_by_category");
        builder.HasIndex(topic => new { topic.AuthorId, topic.CreatedAt })
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ix_topics_active_by_author");

        builder.HasOne(topic => topic.Category).WithMany().HasForeignKey(topic => topic.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(topic => topic.Author).WithMany().HasForeignKey(topic => topic.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(topic => topic.DeletedByUser).WithMany().HasForeignKey(topic => topic.DeletedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
