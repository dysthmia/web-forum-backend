using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Forum.Api.Infrastructure.Persistence;

public class ForumDbContextFactory : IDesignTimeDbContextFactory<ForumDbContext>
{
    public ForumDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ForumDbContext>()
            .UseNpgsql(PostgresConnectionStringFactory.CreateFromEnvironment())
            .Options;

        return new ForumDbContext(options);
    }
}
