using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Forum.Api.Infrastructure.Persistence;

public class ForumDbContext(DbContextOptions<ForumDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<UserBan> UserBans => Set<UserBan>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Topic> Topics => Set<Topic>();

    public DbSet<Post> Posts => Set<Post>();

    public DbSet<Reaction> Reactions => Set<Reaction>();

    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();

    public DbSet<Report> Reports => Set<Report>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ForumDbContext).Assembly);
    }
}
