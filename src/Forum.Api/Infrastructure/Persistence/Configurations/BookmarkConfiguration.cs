using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Forum.Api.Infrastructure.Persistence.Configurations;

public class BookmarkConfiguration : IEntityTypeConfiguration<Bookmark>
{
    public void Configure(EntityTypeBuilder<Bookmark> builder)
    {
        builder.ToTable("bookmarks");

        builder.HasKey(bookmark => new { bookmark.UserId, bookmark.TopicId });
        builder.Property(bookmark => bookmark.UserId).HasColumnName("user_id");
        builder.Property(bookmark => bookmark.TopicId).HasColumnName("topic_id");
        builder.Property(bookmark => bookmark.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();

        builder.HasIndex(bookmark => new { bookmark.UserId, bookmark.CreatedAt })
            .HasDatabaseName("ix_bookmarks_by_user_created_at");
        builder.HasOne(bookmark => bookmark.User).WithMany().HasForeignKey(bookmark => bookmark.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(bookmark => bookmark.Topic).WithMany().HasForeignKey(bookmark => bookmark.TopicId).OnDelete(DeleteBehavior.Restrict);
    }
}
