using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Forum.Api.Infrastructure.Persistence.Configurations;

public class UserBanConfiguration : IEntityTypeConfiguration<UserBan>
{
    public void Configure(EntityTypeBuilder<UserBan> builder)
    {
        builder.ToTable("user_bans", table => table.HasCheckConstraint(
            "ck_user_bans_expiry",
            "expires_at IS NULL OR expires_at > created_at"));

        builder.HasKey(ban => ban.Id);
        builder.Property(ban => ban.Id).UseIdentityByDefaultColumn();
        builder.Property(ban => ban.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(ban => ban.ModeratorId).HasColumnName("moderator_id").IsRequired();
        builder.Property(ban => ban.Reason).HasColumnName("reason").IsRequired();
        builder.Property(ban => ban.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();
        builder.Property(ban => ban.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone");
        builder.Property(ban => ban.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamp with time zone");

        builder.HasIndex(ban => ban.UserId);
        builder.HasOne(ban => ban.User).WithMany().HasForeignKey(ban => ban.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(ban => ban.Moderator).WithMany().HasForeignKey(ban => ban.ModeratorId).OnDelete(DeleteBehavior.Restrict);
    }
}
