using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Forum.Api.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", table => table.HasCheckConstraint(
            "ck_users_role",
            "role IN ('user', 'moderator', 'admin')"));

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        builder.Property(user => user.Username).HasColumnName("username").HasColumnType("varchar(32)").IsRequired();
        builder.Property(user => user.Email).HasColumnName("email").HasColumnType("varchar(254)").IsRequired();
        builder.Property(user => user.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(user => user.Role)
            .HasColumnName("role")
            .HasConversion(
                role => role.ToString().ToLowerInvariant(),
                value => Enum.Parse<UserRole>(value, true))
            .HasColumnType("varchar(16)")
            .IsRequired();
        builder.Property(user => user.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();
    }
}
