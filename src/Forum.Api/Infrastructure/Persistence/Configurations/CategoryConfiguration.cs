using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Forum.Api.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories", table =>
        {
            table.HasCheckConstraint("ck_categories_name_not_blank", "btrim(name) <> ''");
            table.HasCheckConstraint("ck_categories_slug_not_blank", "btrim(slug) <> ''");
        });

        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        builder.Property(category => category.Name).HasColumnName("name").HasColumnType("varchar(100)").IsRequired();
        builder.Property(category => category.Slug).HasColumnName("slug").HasColumnType("varchar(100)").IsRequired();
        builder.Property(category => category.Description).HasColumnName("description").HasColumnType("varchar(1000)");
        builder.HasIndex(category => category.Slug).IsUnique();
    }
}
