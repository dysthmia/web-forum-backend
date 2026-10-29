using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Forum.Api.Infrastructure.Persistence.Configurations;

public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("posts", table => table.HasCheckConstraint("ck_posts_position", "position >= 1"));

        builder.HasKey(post => post.Id);
        builder.Property(post => post.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        builder.Property(post => post.TopicId).HasColumnName("topic_id").IsRequired();
        builder.Property(post => post.AuthorId).HasColumnName("author_id").IsRequired();
        builder.Property(post => post.Position).HasColumnName("position").IsRequired();
        builder.Property(post => post.Content).HasColumnName("content").IsRequired();
        builder.Property(post => post.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();
        builder.Property(post => post.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();
        builder.Property(post => post.DeletedAt).HasColumnName("deleted_at").HasColumnType("timestamp with time zone");
        builder.Property(post => post.DeletedBy).HasColumnName("deleted_by");

        builder.HasIndex(post => new { post.TopicId, post.Position }).IsUnique();
        builder.HasIndex(post => post.AuthorId);

        builder.HasOne(post => post.Topic).WithMany().HasForeignKey(post => post.TopicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(post => post.Author).WithMany().HasForeignKey(post => post.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(post => post.DeletedByUser).WithMany().HasForeignKey(post => post.DeletedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
