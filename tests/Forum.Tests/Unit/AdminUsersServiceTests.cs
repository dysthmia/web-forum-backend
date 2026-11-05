using Forum.Api.Features.Topics;
using Forum.Api.Features.Users;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Forum.Tests.Unit;

public class AdminUsersServiceTests
{
    [Fact]
    public async Task GetListAsync_FiltersByRoleAndIncludesActiveBanState()
    {
        await using var dbContext = CreateDbContext();
        var moderator = await AddUserAsync(dbContext, "moderator", UserRole.Moderator);
        var user = await AddUserAsync(dbContext, "alice", UserRole.User);
        dbContext.UserBans.Add(new UserBan
        {
            UserId = user.Id,
            ModeratorId = moderator.Id,
            Reason = "Temporary",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        await dbContext.SaveChangesAsync();

        var (response, failure) = await CreateService(dbContext).GetListAsync(
            new AdminUserListRequest { Role = "user", Query = "ali", PageSize = 10 }, CancellationToken.None);

        Assert.Equal(AdminUserFailure.None, failure);
        var listedUser = Assert.Single(response!.Items);
        Assert.Equal("alice", listedUser.Username);
        Assert.True(listedUser.IsBanned);
    }

    [Fact]
    public async Task ChangeRoleAsync_RejectsSelfAndAdminTargets()
    {
        await using var dbContext = CreateDbContext();
        var admin = await AddUserAsync(dbContext, "admin", UserRole.Admin);
        var user = await AddUserAsync(dbContext, "user", UserRole.User);
        var service = CreateService(dbContext);

        var self = await service.ChangeRoleAsync(admin.Id, admin.Id, new ChangeUserRoleRequest { Role = "moderator" }, CancellationToken.None);
        var promote = await service.ChangeRoleAsync(admin.Id, user.Id, new ChangeUserRoleRequest { Role = "admin" }, CancellationToken.None);
        var changed = await service.ChangeRoleAsync(admin.Id, user.Id, new ChangeUserRoleRequest { Role = "moderator" }, CancellationToken.None);

        Assert.Equal(AdminUserFailure.Forbidden, self.Failure);
        Assert.Equal(AdminUserFailure.Forbidden, promote.Failure);
        Assert.Equal(AdminUserFailure.None, changed.Failure);
        Assert.Equal(UserRole.Moderator, user.Role);
    }

    private static AdminUsersService CreateService(ForumDbContext dbContext) => new(
        dbContext,
        new TopicPaginationOptions { DefaultPageSize = 10, MaxPageSize = 50 });

    private static ForumDbContext CreateDbContext() => new(new DbContextOptionsBuilder<ForumDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

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
