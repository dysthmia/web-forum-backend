using Forum.Api.Features.Moderation;
using Forum.Api.Features.Topics;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Forum.Tests.Unit;

public class ModerationServiceTests
{
    [Fact]
    public async Task ReviewAsync_AcceptsReportAndOptionallySoftDeletesReply()
    {
        await using var dbContext = CreateDbContext();
        var (report, moderator, post) = await SeedReportAsync(dbContext, postPosition: 2);

        var failure = await CreateService(dbContext).ReviewAsync(report.Id, moderator.Id,
            new ReviewReportRequest { Status = "accepted", HidePost = true }, CancellationToken.None);

        var reviewedReport = await dbContext.Reports.SingleAsync();
        var deletedPost = await dbContext.Posts.SingleAsync();
        Assert.Equal(ReviewReportFailure.None, failure);
        Assert.Equal(ReportStatus.Accepted, reviewedReport.Status);
        Assert.Equal(moderator.Id, reviewedReport.ResolvedBy);
        Assert.NotNull(reviewedReport.ResolvedAt);
        Assert.Equal(moderator.Id, deletedPost.DeletedBy);
        Assert.NotNull(deletedPost.DeletedAt);
    }

    [Fact]
    public async Task ReviewAsync_RejectsReportWithoutChangingPost()
    {
        await using var dbContext = CreateDbContext();
        var (report, moderator, _) = await SeedReportAsync(dbContext, postPosition: 2);

        var failure = await CreateService(dbContext).ReviewAsync(report.Id, moderator.Id,
            new ReviewReportRequest { Status = "rejected", HidePost = false }, CancellationToken.None);

        Assert.Equal(ReviewReportFailure.None, failure);
        Assert.Equal(ReportStatus.Rejected, (await dbContext.Reports.SingleAsync()).Status);
        Assert.Null((await dbContext.Posts.SingleAsync()).DeletedAt);
    }

    [Fact]
    public async Task ReviewAsync_RejectsHidingFirstPostAndLeavesReportPending()
    {
        await using var dbContext = CreateDbContext();
        var (report, moderator, _) = await SeedReportAsync(dbContext, postPosition: 1);

        var failure = await CreateService(dbContext).ReviewAsync(report.Id, moderator.Id,
            new ReviewReportRequest { Status = "accepted", HidePost = true }, CancellationToken.None);

        Assert.Equal(ReviewReportFailure.FirstPostCannotBeHidden, failure);
        Assert.Equal(ReportStatus.Pending, (await dbContext.Reports.SingleAsync()).Status);
        Assert.Null((await dbContext.Posts.SingleAsync()).DeletedAt);
    }

    [Fact]
    public async Task ReviewAsync_IsIdempotentForSameDecisionAndRejectsDifferentDecision()
    {
        await using var dbContext = CreateDbContext();
        var (report, moderator, _) = await SeedReportAsync(dbContext, postPosition: 2);
        var service = CreateService(dbContext);

        var first = await service.ReviewAsync(report.Id, moderator.Id,
            new ReviewReportRequest { Status = "accepted" }, CancellationToken.None);
        var repeated = await service.ReviewAsync(report.Id, moderator.Id,
            new ReviewReportRequest { Status = "accepted" }, CancellationToken.None);
        var changed = await service.ReviewAsync(report.Id, moderator.Id,
            new ReviewReportRequest { Status = "rejected" }, CancellationToken.None);

        Assert.Equal(ReviewReportFailure.None, first);
        Assert.Equal(ReviewReportFailure.None, repeated);
        Assert.Equal(ReviewReportFailure.AlreadyResolved, changed);
    }

    [Fact]
    public async Task GetPendingReportsAsync_ReturnsOnlyPendingReports()
    {
        await using var dbContext = CreateDbContext();
        var (pending, moderator, _) = await SeedReportAsync(dbContext, postPosition: 2);
        var (resolved, _, _) = await SeedReportAsync(dbContext, postPosition: 2, suffix: "resolved");
        resolved.Status = ReportStatus.Accepted;
        resolved.ResolvedBy = moderator.Id;
        resolved.ResolvedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var reports = await CreateService(dbContext).GetPendingReportsAsync(CancellationToken.None);

        Assert.Single(reports);
        Assert.Equal(pending.Id, reports[0].ReportId);
    }

    private static async Task<(Report Report, User Moderator, Post Post)> SeedReportAsync(
        ForumDbContext dbContext,
        int postPosition,
        string suffix = "pending")
    {
        var category = new Category { Name = $"General-{suffix}", Slug = $"general-{suffix}" };
        var reporter = CreateUser($"reporter-{suffix}", $"reporter-{suffix}@example.com");
        var moderator = CreateUser($"moderator-{suffix}", $"moderator-{suffix}@example.com", UserRole.Moderator);
        dbContext.AddRange(category, reporter, moderator);
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
            Position = postPosition,
            Content = "Post",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Posts.Add(post);
        await dbContext.SaveChangesAsync();
        var report = new Report
        {
            ReporterId = reporter.Id,
            PostId = post.Id,
            Reason = "Reason",
            Status = ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Reports.Add(report);
        await dbContext.SaveChangesAsync();
        return (report, moderator, post);
    }

    private static ForumDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ForumDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ForumDbContext(options);
    }

    private static ModerationService CreateService(ForumDbContext dbContext) => new(
        dbContext,
        new TopicPaginationOptions { DefaultPageSize = 10, MaxPageSize = 50 });

    private static User CreateUser(string username, string email, UserRole role = UserRole.User) => new()
    {
        Username = username,
        Email = email,
        PasswordHash = "hash",
        Role = role,
        CreatedAt = DateTime.UtcNow
    };
}
