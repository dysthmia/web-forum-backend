using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using StackExchange.Redis;

namespace Forum.Api.Infrastructure.Redis;

public sealed class ViewCounterService(
    IConnectionMultiplexer connectionMultiplexer,
    RedisOptions options,
    ILogger<ViewCounterService> logger)
{
    private const string RecordViewScript = """
        if redis.call('SET', KEYS[1], '1', 'NX', 'EX', ARGV[1]) then
            redis.call('INCR', KEYS[2])
            redis.call('SADD', KEYS[3], ARGV[2])
            return 1
        end
        return 0
        """;

    private const string TakePendingViewsScript = """
        local value = redis.call('GET', KEYS[1])
        if not value then
            redis.call('SREM', KEYS[2], ARGV[1])
            return 0
        end
        redis.call('DEL', KEYS[1])
        redis.call('SREM', KEYS[2], ARGV[1])
        return value
        """;

    public async Task<bool> RecordViewAsync(long topicId, HttpContext httpContext, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topicId);

        var visitorHash = GetVisitorHash(httpContext);
        var database = connectionMultiplexer.GetDatabase();
        var result = await database.ScriptEvaluateAsync(
            RecordViewScript,
            [options.GetSeenKey(topicId, visitorHash), options.GetCounterKey(topicId), options.ViewDirtyTopicsKey],
            [(int)options.ViewWindow.TotalSeconds, topicId]).WaitAsync(cancellationToken);

        return (int)result == 1;
    }

    public async Task TryRecordViewAsync(long topicId, HttpContext httpContext, CancellationToken cancellationToken)
    {
        try
        {
            await RecordViewAsync(topicId, httpContext, cancellationToken);
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Unable to record a topic view.");
        }
    }

    public async IAsyncEnumerable<long> GetDirtyTopicIdsAsync()
    {
        var database = connectionMultiplexer.GetDatabase();
        await foreach (var value in database.SetScanAsync(options.ViewDirtyTopicsKey, pageSize: 100))
        {
            if (long.TryParse(value.ToString(), CultureInfo.InvariantCulture, out var topicId) && topicId > 0)
            {
                yield return topicId;
            }
        }
    }

    public async Task<long> TakePendingViewsAsync(long topicId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topicId);

        var database = connectionMultiplexer.GetDatabase();
        var result = await database.ScriptEvaluateAsync(
            TakePendingViewsScript,
            [options.GetCounterKey(topicId), options.ViewDirtyTopicsKey],
            [topicId]).WaitAsync(cancellationToken);

        return long.TryParse(result.ToString(), CultureInfo.InvariantCulture, out var views) ? views : 0;
    }

    private string GetVisitorHash(HttpContext httpContext)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var visitorId = long.TryParse(userId, out var parsedUserId) && parsedUserId > 0
            ? $"user:{parsedUserId}"
            : $"guest:{GetOrCreateVisitorId(httpContext)}";

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(visitorId))).ToLowerInvariant();
    }

    private string GetOrCreateVisitorId(HttpContext httpContext)
    {
        if (httpContext.Request.Cookies.TryGetValue(options.VisitorCookieName, out var existingVisitorId)
            && Guid.TryParseExact(existingVisitorId, "N", out _))
        {
            return existingVisitorId.ToLowerInvariant();
        }

        var visitorId = Guid.NewGuid().ToString("N");
        httpContext.Response.Cookies.Append(options.VisitorCookieName, visitorId, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.Add(options.VisitorCookieLifetime),
            HttpOnly = true,
            IsEssential = true,
            SameSite = options.VisitorCookieSameSite,
            Secure = options.VisitorCookieSecure
        });
        return visitorId;
    }
}
