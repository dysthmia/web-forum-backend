using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Forum.Api.Infrastructure.Persistence;

public class ForumDbContextFactory : IDesignTimeDbContextFactory<ForumDbContext>
{
    public ForumDbContext CreateDbContext(string[] args)
    {
        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? throw new InvalidOperationException("POSTGRES_HOST is required.");
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? throw new InvalidOperationException("POSTGRES_PORT is required.");
        var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? throw new InvalidOperationException("POSTGRES_USER is required.");
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? throw new InvalidOperationException("POSTGRES_PASSWORD is required.");
        var database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? throw new InvalidOperationException("POSTGRES_DB is required.");

        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = int.Parse(port),
            Username = username,
            Password = password,
            Database = database
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<ForumDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ForumDbContext(options);
    }
}
