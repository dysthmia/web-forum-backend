using Forum.Api.Features.Moderation;
using Forum.Api.Features.Topics;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Forum.Tests.Unit;

public class UserBanServiceTests
{
    [Fact]
    public async Task CreateBanAsync_ModeratorCreatesTemporaryBanAndRejectsSecondActiveBan()
    {
        await using var dbContext = CreateDbContext();
        var moderator = await AddUserAsync(dbContext, "moderator", UserRole.Moderator);
        var user = await AddUserAsync(dbContext, "user", UserRole.User);
        var service = CreateService(dbContext);
        var request = new CreateBanRequest
        {
            UserId = user.Id,
            Reason = " Rule violation ",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        var created = await service.CreateBanAsync(new BanActor(moderator.Id, false), request, CancellationToken.None);
        var duplicate = await service.CreateBanAsync(new BanActor(moderator.Id, false), request, CancellationToken.None);

        var ban = await dbContext.UserBans.SingleAsync();
        Assert.Equal(BanFailure.None, created.Failure);
        Assert.Equal(BanFailure.ActiveBanExists, duplicate.Failure);
        Assert.Equal("Rule violation", ban.Reason);
        Assert.Equal(moderator.Id, ban.ModeratorId);
    }

    [Fact]
    public async Task CreateBanAsync_ExpiredBanDoesNotPreventNewBan()
    {
        await using var dbContext = CreateDbContext();
        var moderator = await AddUserAsync(dbContext, "moderator", UserRole.Moderator);
        var user = await AddUserAsync(dbContext, "user", UserRole.User);
        dbContext.UserBans.Add(new UserBan
        {
            UserId = user.Id,
            ModeratorId = moderator.Id,
            Reason = "Expired",
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        });
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext).CreateBanAsync(
            new BanActor(moderator.Id, false),
            new CreateBanRequest
            {
                UserId = user.Id,
                Reason = "New ban",
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            },
            CancellationToken.None);

        Assert.Equal(BanFailure.None, result.Failure);
        Assert.Equal(2, await dbContext.UserBans.CountAsync());
    }

    [Fact]
    public async Task CreateBanAsync_EnforcesRoleRulesForModeratorAndAdmin()
    {
        await using var dbContext = CreateDbContext();
        var moderator = await AddUserAsync(dbContext, "moderator", UserRole.Moderator);
        var administrator = await AddUserAsync(dbContext, "administrator", UserRole.Admin);
        var anotherModerator = await AddUserAsync(dbContext, "another-moderator", UserRole.Moderator);
        var user = await AddUserAsync(dbContext, "user", UserRole.User);
        var service = CreateService(dbContext);

        var permanentByModerator = await service.CreateBanAsync(
            new BanActor(moderator.Id, false),
            new CreateBanRequest { UserId = user.Id, Reason = "Permanent" },
            CancellationToken.None);
        var moderatorTarget = await service.CreateBanAsync(
            new BanActor(moderator.Id, false),
            new CreateBanRequest
            {
                UserId = anotherModerator.Id,
                Reason = "Temporary",
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            },
            CancellationToken.None);
        var administratorTarget = await service.CreateBanAsync(
            new BanActor(moderator.Id, false),
            new CreateBanRequest
            {
                UserId = administrator.Id,
                Reason = "Temporary",
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            },
            CancellationToken.None);
        var permanentByAdmin = await service.CreateBanAsync(
            new BanActor(administrator.Id, true),
            new CreateBanRequest { UserId = anotherModerator.Id, Reason = "Permanent" },
            CancellationToken.None);

        Assert.Equal(BanFailure.Forbidden, permanentByModerator.Failure);
        Assert.Equal(BanFailure.Forbidden, moderatorTarget.Failure);
        Assert.Equal(BanFailure.Forbidden, administratorTarget.Failure);
        Assert.Equal(BanFailure.None, permanentByAdmin.Failure);
        Assert.Null(permanentByAdmin.Response!.ExpiresAt);
    }

    [Fact]
    public async Task RevokeBanAsync_RevokesActiveBanAndReturnsHistory()
    {
        await using var dbContext = CreateDbContext();
        var moderator = await AddUserAsync(dbContext, "moderator", UserRole.Moderator);
        var user = await AddUserAsync(dbContext, "user", UserRole.User);
        var service = CreateService(dbContext);
        var created = await service.CreateBanAsync(
            new BanActor(moderator.Id, false),
            new CreateBanRequest
            {
                UserId = user.Id,
                Reason = "Temporary",
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            },
            CancellationToken.None);

        var banId = Assert.IsType<BanResponse>(created.Response).BanId;
        var revoked = await service.RevokeBanAsync(
            banId,
            new BanActor(moderator.Id, false),
            CancellationToken.None);
        var repeated = await service.RevokeBanAsync(
            banId,
            new BanActor(moderator.Id, false),
            CancellationToken.None);
        var (history, failure) = await service.GetBanHistoryAsync(
            user.Id,
            new BanActor(moderator.Id, false),
            CancellationToken.None);

        Assert.Equal(BanFailure.None, revoked);
        Assert.Equal(BanFailure.AlreadyRevoked, repeated);
        Assert.Equal(BanFailure.None, failure);
        var historyEntry = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<BanHistoryResponse>>(history));
        Assert.Equal("user", historyEntry.Username);
        Assert.Equal("moderator", historyEntry.ModeratorUsername);
        Assert.NotNull(historyEntry.RevokedAt);
    }

    [Fact]
    public async Task GetBansAsync_FiltersActiveExpiredAndRevokedBans()
    {
        await using var dbContext = CreateDbContext();
        var moderator = await AddUserAsync(dbContext, "moderator", UserRole.Moderator);
        var user = await AddUserAsync(dbContext, "user", UserRole.User);
        var now = DateTime.UtcNow;
        dbContext.UserBans.AddRange(
            new UserBan { UserId = user.Id, ModeratorId = moderator.Id, Reason = "Active", CreatedAt = now, ExpiresAt = now.AddHours(1) },
            new UserBan { UserId = user.Id, ModeratorId = moderator.Id, Reason = "Expired", CreatedAt = now.AddHours(-2), ExpiresAt = now.AddHours(-1) },
            new UserBan { UserId = user.Id, ModeratorId = moderator.Id, Reason = "Revoked", CreatedAt = now, RevokedAt = now });
        await dbContext.SaveChangesAsync();

        var (response, failure) = await CreateService(dbContext).GetBansAsync(
            new BanListRequest { Query = "user", Status = "active", PageSize = 10 }, CancellationToken.None);

        Assert.Equal(BanFailure.None, failure);
        var ban = Assert.Single(response!.Items);
        Assert.Equal("Active", ban.Reason);
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

    private static async Task<User> AddUserAsync(ForumDbContext dbContext, string username, UserRole role)
    {
        var user = new User
        {
            Username = username,
            Email = $"{username}@example.com",
            PasswordHash = "hash",
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }
}
