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
        Assert.Contains("/api/users/me", document);
        Assert.Contains("/api/topics", document);
        Assert.Contains("/api/topics/{topicId}/posts", document);
        Assert.Contains("/api/posts/{postId}/reactions", document);
        Assert.Contains("/api/reactions/counts", document);
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
            ["JWT_ISSUER"] = "forum-tests",
            ["JWT_AUDIENCE"] = "forum-tests",
            ["JWT_SECRET"] = "01234567890123456789012345678901",
            ["JWT_ACCESS_TOKEN_MINUTES"] = "15",
            ["JWT_REFRESH_TOKEN_DAYS"] = "30",
            ["PASSWORD_HASH_ITERATIONS"] = "10000",
            ["TOPICS_DEFAULT_PAGE_SIZE"] = "20",
            ["TOPICS_MAX_PAGE_SIZE"] = "50"
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
