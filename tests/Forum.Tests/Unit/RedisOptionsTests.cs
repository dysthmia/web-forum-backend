using Forum.Api.Infrastructure.Redis;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Forum.Tests.Unit;

public class RedisOptionsTests
{
    [Fact]
    public void Create_ReadsViewAndCookieSettings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["REDIS_HOST"] = "redis",
                ["REDIS_PORT"] = "6379",
                ["REDIS_CONNECT_TIMEOUT_MS"] = "5000",
                ["REDIS_SYNC_TIMEOUT_MS"] = "5000",
                ["VIEW_WINDOW_MINUTES"] = "30",
                ["VIEW_FLUSH_INTERVAL_SECONDS"] = "60",
                ["CACHE_TTL_SECONDS"] = "300",
                ["CACHE_KEY_PREFIX"] = "forum:cache",
                ["RATE_LIMIT_KEY_PREFIX"] = "forum:rate-limit",
                ["LOGIN_RATE_LIMIT_REQUESTS"] = "5",
                ["LOGIN_RATE_LIMIT_WINDOW_SECONDS"] = "60",
                ["CONTENT_RATE_LIMIT_REQUESTS"] = "20",
                ["CONTENT_RATE_LIMIT_WINDOW_SECONDS"] = "60",
                ["VIEW_SEEN_KEY_PREFIX"] = "forum:view:seen",
                ["VIEW_COUNTER_KEY_PREFIX"] = "forum:view:counter",
                ["VIEW_DIRTY_TOPICS_KEY"] = "forum:view:dirty",
                ["VISITOR_COOKIE_NAME"] = "forum_visitor",
                ["VISITOR_COOKIE_DAYS"] = "365",
                ["VISITOR_COOKIE_SAME_SITE"] = "Lax",
                ["VISITOR_COOKIE_SECURE"] = "false"
            })
            .Build();

        var options = RedisOptionsFactory.Create(configuration);

        Assert.Equal("redis", options.Host);
        Assert.Equal(TimeSpan.FromMinutes(30), options.ViewWindow);
        Assert.Equal(TimeSpan.FromSeconds(60), options.ViewFlushInterval);
        Assert.False(options.VisitorCookieSecure);
        Assert.Equal("forum:view:counter:42", options.GetCounterKey(42));
    }

    [Fact]
    public void Create_RejectsInvalidCookieSameSite()
    {
        var settings = CreateSettings();
        settings["VISITOR_COOKIE_SAME_SITE"] = "Invalid";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() => RedisOptionsFactory.Create(configuration));

        Assert.Contains("VISITOR_COOKIE_SAME_SITE", exception.Message);
    }

    private static Dictionary<string, string?> CreateSettings() => new()
    {
        ["REDIS_HOST"] = "redis",
        ["REDIS_PORT"] = "6379",
        ["REDIS_CONNECT_TIMEOUT_MS"] = "5000",
        ["REDIS_SYNC_TIMEOUT_MS"] = "5000",
        ["VIEW_WINDOW_MINUTES"] = "30",
        ["VIEW_FLUSH_INTERVAL_SECONDS"] = "60",
        ["CACHE_TTL_SECONDS"] = "300",
        ["CACHE_KEY_PREFIX"] = "forum:cache",
        ["RATE_LIMIT_KEY_PREFIX"] = "forum:rate-limit",
        ["LOGIN_RATE_LIMIT_REQUESTS"] = "5",
        ["LOGIN_RATE_LIMIT_WINDOW_SECONDS"] = "60",
        ["CONTENT_RATE_LIMIT_REQUESTS"] = "20",
        ["CONTENT_RATE_LIMIT_WINDOW_SECONDS"] = "60",
        ["VIEW_SEEN_KEY_PREFIX"] = "forum:view:seen",
        ["VIEW_COUNTER_KEY_PREFIX"] = "forum:view:counter",
        ["VIEW_DIRTY_TOPICS_KEY"] = "forum:view:dirty",
        ["VISITOR_COOKIE_NAME"] = "forum_visitor",
        ["VISITOR_COOKIE_DAYS"] = "365",
        ["VISITOR_COOKIE_SAME_SITE"] = "Lax",
        ["VISITOR_COOKIE_SECURE"] = "false"
    };
}
