using Microsoft.AspNetCore.Http;
using StackExchange.Redis;

namespace Forum.Api.Infrastructure.Redis;

public sealed class RedisOptions
{
    public required string Host { get; init; }

    public required int Port { get; init; }

    public required int ConnectTimeoutMilliseconds { get; init; }

    public required int SyncTimeoutMilliseconds { get; init; }

    public required TimeSpan ViewWindow { get; init; }

    public required TimeSpan ViewFlushInterval { get; init; }

    public required TimeSpan CacheTtl { get; init; }

    public required int LoginRateLimitRequests { get; init; }

    public required TimeSpan LoginRateLimitWindow { get; init; }

    public required int ContentRateLimitRequests { get; init; }

    public required TimeSpan ContentRateLimitWindow { get; init; }

    public required string CacheKeyPrefix { get; init; }

    public required string RateLimitKeyPrefix { get; init; }

    public required string ViewSeenKeyPrefix { get; init; }

    public required string ViewCounterKeyPrefix { get; init; }

    public required string ViewDirtyTopicsKey { get; init; }

    public required string VisitorCookieName { get; init; }

    public required TimeSpan VisitorCookieLifetime { get; init; }

    public required SameSiteMode VisitorCookieSameSite { get; init; }

    public required bool VisitorCookieSecure { get; init; }

    public ConfigurationOptions CreateConnectionOptions()
    {
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectRetry = 1,
            ConnectTimeout = ConnectTimeoutMilliseconds,
            SyncTimeout = SyncTimeoutMilliseconds
        };
        options.EndPoints.Add(Host, Port);
        return options;
    }

    public RedisKey GetSeenKey(long topicId, string visitorHash) => $"{ViewSeenKeyPrefix}:{topicId}:{visitorHash}";

    public RedisKey GetCounterKey(long topicId) => $"{ViewCounterKeyPrefix}:{topicId}";
}

public static class RedisOptionsFactory
{
    public static RedisOptions Create(IConfiguration configuration)
    {
        var viewWindowMinutes = GetPositiveInt(configuration, "VIEW_WINDOW_MINUTES");
        var cookieDays = GetPositiveInt(configuration, "VISITOR_COOKIE_DAYS");

        if (!Enum.TryParse<SameSiteMode>(Require(configuration, "VISITOR_COOKIE_SAME_SITE"), true, out var sameSite))
        {
            throw new InvalidOperationException("VISITOR_COOKIE_SAME_SITE must be Lax, Strict, or None.");
        }

        return new RedisOptions
        {
            Host = Require(configuration, "REDIS_HOST"),
            Port = GetPort(configuration, "REDIS_PORT"),
            ConnectTimeoutMilliseconds = GetPositiveInt(configuration, "REDIS_CONNECT_TIMEOUT_MS"),
            SyncTimeoutMilliseconds = GetPositiveInt(configuration, "REDIS_SYNC_TIMEOUT_MS"),
            ViewWindow = TimeSpan.FromMinutes(viewWindowMinutes),
            ViewFlushInterval = TimeSpan.FromSeconds(GetPositiveInt(configuration, "VIEW_FLUSH_INTERVAL_SECONDS")),
            CacheTtl = TimeSpan.FromSeconds(GetPositiveInt(configuration, "CACHE_TTL_SECONDS")),
            LoginRateLimitRequests = GetPositiveInt(configuration, "LOGIN_RATE_LIMIT_REQUESTS"),
            LoginRateLimitWindow = TimeSpan.FromSeconds(GetPositiveInt(configuration, "LOGIN_RATE_LIMIT_WINDOW_SECONDS")),
            ContentRateLimitRequests = GetPositiveInt(configuration, "CONTENT_RATE_LIMIT_REQUESTS"),
            ContentRateLimitWindow = TimeSpan.FromSeconds(GetPositiveInt(configuration, "CONTENT_RATE_LIMIT_WINDOW_SECONDS")),
            CacheKeyPrefix = Require(configuration, "CACHE_KEY_PREFIX"),
            RateLimitKeyPrefix = Require(configuration, "RATE_LIMIT_KEY_PREFIX"),
            ViewSeenKeyPrefix = Require(configuration, "VIEW_SEEN_KEY_PREFIX"),
            ViewCounterKeyPrefix = Require(configuration, "VIEW_COUNTER_KEY_PREFIX"),
            ViewDirtyTopicsKey = Require(configuration, "VIEW_DIRTY_TOPICS_KEY"),
            VisitorCookieName = Require(configuration, "VISITOR_COOKIE_NAME"),
            VisitorCookieLifetime = TimeSpan.FromDays(cookieDays),
            VisitorCookieSameSite = sameSite,
            VisitorCookieSecure = GetBoolean(configuration, "VISITOR_COOKIE_SECURE")
        };
    }

    private static int GetPort(IConfiguration configuration, string key)
    {
        var value = GetPositiveInt(configuration, key);
        if (value > 65535)
        {
            throw new InvalidOperationException($"{key} must be between 1 and 65535.");
        }

        return value;
    }

    private static int GetPositiveInt(IConfiguration configuration, string key)
    {
        if (!int.TryParse(configuration[key], out var value) || value <= 0)
        {
            throw new InvalidOperationException($"{key} must be a positive integer.");
        }

        return value;
    }

    private static bool GetBoolean(IConfiguration configuration, string key)
    {
        if (!bool.TryParse(configuration[key], out var value))
        {
            throw new InvalidOperationException($"{key} must be true or false.");
        }

        return value;
    }

    private static string Require(IConfiguration configuration, string key) =>
        string.IsNullOrWhiteSpace(configuration[key])
            ? throw new InvalidOperationException($"{key} is required.")
            : configuration[key]!;
}
