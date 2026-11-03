using Forum.Api.Common.Authorization;
using Forum.Api.Features.Reports;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Forum.Tests.Unit;

public class ReportsServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesPendingReportAndRejectsDuplicate()
    {
        await using var dbContext = CreateDbContext();
        var (post, reporter, _) = await SeedPostAsync(dbContext);
        var service = CreateService(dbContext);

        var created = await service.CreateAsync(reporter.Id,
            new CreateReportRequest { PostId = post.Id, Reason = " Spam " }, CancellationToken.None);
        var duplicate = await service.CreateAsync(reporter.Id,
            new CreateReportRequest { PostId = post.Id, Reason = "Again" }, CancellationToken.None);

        Assert.Equal(CreateReportFailure.None, created.Failure);
        Assert.Equal("pending", created.Response!.Status);
        Assert.Equal(CreateReportFailure.DuplicateReport, duplicate.Failure);
        var report = await dbContext.Reports.SingleAsync();
        Assert.Equal("Spam", report.Reason);
        Assert.Equal(ReportStatus.Pending, report.Status);
        Assert.Null(report.ResolvedBy);
        Assert.Null(report.ResolvedAt);
    }

    [Fact]
    public async Task CreateAsync_RejectsDeletedPostAndPostFromDeletedTopic()
    {
        await using var dbContext = CreateDbContext();
        var (post, reporter, topic) = await SeedPostAsync(dbContext);
        var service = CreateService(dbContext);
        post.DeletedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var deletedPost = await service.CreateAsync(reporter.Id,
            new CreateReportRequest { PostId = post.Id, Reason = "Reason" }, CancellationToken.None);

        post.DeletedAt = null;
        topic.DeletedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();
        var deletedTopic = await service.CreateAsync(reporter.Id,
            new CreateReportRequest { PostId = post.Id, Reason = "Reason" }, CancellationToken.None);

        Assert.Equal(CreateReportFailure.PostNotFound, deletedPost.Failure);
        Assert.Equal(CreateReportFailure.PostNotFound, deletedTopic.Failure);
        Assert.Empty(dbContext.Reports);
    }

    [Fact]
    public async Task CreateAsync_RejectsBannedReporter()
    {
        await using var dbContext = CreateDbContext();
        var (post, reporter, _) = await SeedPostAsync(dbContext);
        var moderator = CreateUser("moderator", "moderator@example.com");
        dbContext.Users.Add(moderator);
        await dbContext.SaveChangesAsync();
        dbContext.UserBans.Add(new UserBan
        {
            UserId = reporter.Id,
            ModeratorId = moderator.Id,
            Reason = "Test ban",
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext).CreateAsync(reporter.Id,
            new CreateReportRequest { PostId = post.Id, Reason = "Reason" }, CancellationToken.None);

        Assert.Equal(CreateReportFailure.Banned, result.Failure);
        Assert.Empty(dbContext.Reports);
    }

    private static ReportsService CreateService(ForumDbContext dbContext) =>
        new(dbContext, new ActiveUserBanService(dbContext));

    private static async Task<(Post Post, User Reporter, Topic Topic)> SeedPostAsync(ForumDbContext dbContext)
    {
        var category = new Category { Name = "General", Slug = "general" };
        var reporter = CreateUser("reporter", "reporter@example.com");
        dbContext.AddRange(category, reporter);
        await dbContext.SaveChangesAsync();
        var topic = new Topic
        {
            CategoryId = category.Id,
            AuthorId = reporter.Id,
            Title = "Topic",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Topics.Add(topic);
        await dbContext.SaveChangesAsync();
        var post = new Post
        {
            TopicId = topic.Id,
            AuthorId = reporter.Id,
            Position = 1,
            Content = "Post",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Posts.Add(post);
        await dbContext.SaveChangesAsync();
        return (post, reporter, topic);
    }

    private static ForumDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ForumDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ForumDbContext(options);
    }

    private static User CreateUser(string username, string email) => new()
    {
        Username = username,
        Email = email,
        PasswordHash = "hash",
        Role = UserRole.User,
        CreatedAt = DateTime.UtcNow
    };
}
