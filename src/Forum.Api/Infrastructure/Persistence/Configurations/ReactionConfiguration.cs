using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Forum.Api.Infrastructure.Persistence.Configurations;

public class ReactionConfiguration : IEntityTypeConfiguration<Reaction>
{
    public void Configure(EntityTypeBuilder<Reaction> builder)
    {
        builder.ToTable("reactions", table => table.HasCheckConstraint("ck_reactions_type", "type IN (-1, 1)"));

        builder.HasKey(reaction => new { reaction.UserId, reaction.PostId });
        builder.Property(reaction => reaction.UserId).HasColumnName("user_id");
        builder.Property(reaction => reaction.PostId).HasColumnName("post_id");
        builder.Property(reaction => reaction.Type).HasColumnName("type").HasConversion<int>().IsRequired();
        builder.Property(reaction => reaction.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();

        builder.HasIndex(reaction => new { reaction.PostId, reaction.Type });
        builder.HasOne(reaction => reaction.User).WithMany().HasForeignKey(reaction => reaction.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(reaction => reaction.Post).WithMany().HasForeignKey(reaction => reaction.PostId).OnDelete(DeleteBehavior.Restrict);
    }
}
