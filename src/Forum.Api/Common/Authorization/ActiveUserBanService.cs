using Forum.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Forum.Api.Common.Authorization;

public class ActiveUserBanService(ForumDbContext dbContext)
{
    public Task<bool> IsBannedAsync(long userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return dbContext.UserBans.AsNoTracking().AnyAsync(
            ban => ban.UserId == userId
                   && ban.RevokedAt == null
                   && (ban.ExpiresAt == null || ban.ExpiresAt > now),
            cancellationToken);
    }
}
