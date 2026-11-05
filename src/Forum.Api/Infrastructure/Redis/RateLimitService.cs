using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;

namespace Forum.Api.Infrastructure.Redis;

public enum RateLimitStatus
{
    Allowed,
    Limited,
    Unavailable
}

public record RateLimitResult(RateLimitStatus Status, TimeSpan RetryAfter)
{
    public static RateLimitResult Allowed() => new(RateLimitStatus.Allowed, TimeSpan.Zero);

    public static RateLimitResult Limited(TimeSpan retryAfter) => new(RateLimitStatus.Limited, retryAfter);

    public static RateLimitResult Unavailable() => new(RateLimitStatus.Unavailable, TimeSpan.Zero);
}

public sealed class RateLimitService(
    IConnectionMultiplexer connectionMultiplexer,
    RedisOptions options,
    ILogger<RateLimitService> logger)
{
    private const string IncrementScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
            redis.call('EXPIRE', KEYS[1], ARGV[1])
        end
        return tostring(count) .. ':' .. tostring(redis.call('TTL', KEYS[1]))
        """;

    public Task<RateLimitResult> CheckLoginAsync(string? usernameOrEmail, CancellationToken cancellationToken)
    {
        var normalizedIdentity = usernameOrEmail?.Trim().ToLowerInvariant() ?? string.Empty;
        var identityHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedIdentity))).ToLowerInvariant();
        return CheckAsync(
            $"{options.RateLimitKeyPrefix}:login:{identityHash}",
            options.LoginRateLimitRequests,
            options.LoginRateLimitWindow,
            cancellationToken);
    }

    public Task<RateLimitResult> CheckContentCreationAsync(long userId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        return CheckAsync(
            $"{options.RateLimitKeyPrefix}:content:{userId}",
            options.ContentRateLimitRequests,
            options.ContentRateLimitWindow,
            cancellationToken);
    }

    private async Task<RateLimitResult> CheckAsync(
        RedisKey key,
        int limit,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await connectionMultiplexer.GetDatabase().ScriptEvaluateAsync(
                IncrementScript,
                [key],
                [(int)window.TotalSeconds]).WaitAsync(cancellationToken);
            var values = result.ToString().Split(':', 2);
            if (values.Length != 2
                || !int.TryParse(values[0], CultureInfo.InvariantCulture, out var count)
                || !int.TryParse(values[1], CultureInfo.InvariantCulture, out var ttl))
            {
                return RateLimitResult.Unavailable();
            }

            return count <= limit
                ? RateLimitResult.Allowed()
                : RateLimitResult.Limited(TimeSpan.FromSeconds(Math.Max(ttl, 1)));
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Rate limiting is unavailable.");
            return RateLimitResult.Unavailable();
        }
    }
}
