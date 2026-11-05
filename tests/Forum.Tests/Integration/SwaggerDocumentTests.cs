using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Forum.Tests.Integration;

public class SwaggerDocumentTests : IClassFixture<SwaggerDocumentTests.ForumApplicationFactory>
{
    private readonly HttpClient _client;

    public SwaggerDocumentTests(ForumApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SwaggerDocument_IncludesAuthenticationAndUserProfileEndpoints()
    {
        var document = await _client.GetStringAsync("/swagger/v1/swagger.json");
        using var swaggerUiResponse = await _client.GetAsync("/swagger/index.html");

        Assert.Contains("/api/auth/register", document);
        Assert.Contains("/api/auth/login", document);
        Assert.Contains("/api/auth/refresh", document);
        Assert.Contains("/api/auth/logout", document);
        Assert.Contains("/api/users/{userId}", document);
        Assert.Contains("/api/users/by-username/{username}", document);
        Assert.Contains("/api/users/me", document);
        Assert.Contains("/api/topics", document);
        Assert.Contains("/api/topics/{topicId}/posts", document);
        Assert.Contains("/api/posts/{postId}/reactions", document);
        Assert.Contains("/api/reactions/counts", document);
        Assert.Contains("/api/bookmarks", document);
        Assert.Contains("/api/bookmarks/{topicId}", document);
        Assert.Contains("currentUserReaction", document);
        Assert.Contains("/api/reports", document);
        Assert.Contains("/api/moderation/reports", document);
        Assert.Contains("/api/moderation/bans", document);
        Assert.Contains("/api/moderation/users/{userId}/bans", document);
        Assert.Contains("/api/search", document);
        Assert.True(swaggerUiResponse.IsSuccessStatusCode);
    }

    public class ForumApplicationFactory : WebApplicationFactory<Program>
    {
        private static readonly IReadOnlyDictionary<string, string> Settings = new Dictionary<string, string>
        {
            ["POSTGRES_HOST"] = "localhost",
            ["POSTGRES_PORT"] = "5432",
            ["POSTGRES_USER"] = "forum_user",
            ["POSTGRES_PASSWORD"] = "test-password",
            ["POSTGRES_DB"] = "forum",
            ["REDIS_HOST"] = "localhost",
            ["REDIS_PORT"] = "6379",
            ["REDIS_CONNECT_TIMEOUT_MS"] = "100",
            ["REDIS_SYNC_TIMEOUT_MS"] = "100",
            ["JWT_ISSUER"] = "forum-tests",
            ["JWT_AUDIENCE"] = "forum-tests",
            ["JWT_SECRET"] = "01234567890123456789012345678901",
            ["JWT_ACCESS_TOKEN_MINUTES"] = "15",
            ["JWT_REFRESH_TOKEN_DAYS"] = "30",
            ["REFRESH_COOKIE_NAME"] = "test_refresh",
            ["REFRESH_COOKIE_SAME_SITE"] = "Lax",
            ["REFRESH_COOKIE_SECURE"] = "false",
            ["CORS_ALLOWED_ORIGINS"] = "http://localhost:3000",
            ["PASSWORD_HASH_ITERATIONS"] = "10000",
            ["TOPICS_DEFAULT_PAGE_SIZE"] = "20",
            ["TOPICS_MAX_PAGE_SIZE"] = "50",
            ["VIEW_WINDOW_MINUTES"] = "30",
            ["VIEW_FLUSH_INTERVAL_SECONDS"] = "60",
            ["CACHE_TTL_SECONDS"] = "300",
            ["CACHE_KEY_PREFIX"] = "test:cache",
            ["VIEW_SEEN_KEY_PREFIX"] = "test:view:seen",
            ["VIEW_COUNTER_KEY_PREFIX"] = "test:view:counter",
            ["VIEW_DIRTY_TOPICS_KEY"] = "test:view:dirty",
            ["VISITOR_COOKIE_NAME"] = "test_visitor",
            ["VISITOR_COOKIE_DAYS"] = "365",
            ["VISITOR_COOKIE_SAME_SITE"] = "Lax",
            ["VISITOR_COOKIE_SECURE"] = "false",
            ["RATE_LIMIT_KEY_PREFIX"] = "test:rate-limit",
            ["LOGIN_RATE_LIMIT_REQUESTS"] = "5",
            ["LOGIN_RATE_LIMIT_WINDOW_SECONDS"] = "60",
            ["CONTENT_RATE_LIMIT_REQUESTS"] = "20",
            ["CONTENT_RATE_LIMIT_WINDOW_SECONDS"] = "60"
        };

        private readonly Dictionary<string, string?> _originalEnvironmentValues = new();

        public ForumApplicationFactory()
        {
            foreach (var (key, value) in Settings)
            {
                _originalEnvironmentValues[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var (key, value) in _originalEnvironmentValues)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }

            base.Dispose(disposing);
        }
    }
}
