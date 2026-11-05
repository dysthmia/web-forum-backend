using Forum.Api.Features.Auth;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Forum.Tests.Unit;

public class RefreshSessionOptionsTests
{
    [Fact]
    public void Create_ReadsCookieAndCorsSettings()
    {
        var options = RefreshSessionOptionsFactory.Create(CreateConfiguration());

        Assert.Equal("forum_refresh", options.CookieName);
        Assert.Equal(Microsoft.AspNetCore.Http.SameSiteMode.Lax, options.CookieSameSite);
        Assert.False(options.CookieSecure);
        Assert.Equal(["http://localhost:3000", "https://forum.example.com"], options.AllowedOrigins);
    }

    [Fact]
    public void Create_RejectsInsecureCrossSiteCookie()
    {
        var settings = CreateSettings();
        settings["REFRESH_COOKIE_SAME_SITE"] = "None";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RefreshSessionOptionsFactory.Create(new ConfigurationBuilder().AddInMemoryCollection(settings).Build()));

        Assert.Contains("REFRESH_COOKIE_SECURE", exception.Message);
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(CreateSettings()).Build();

    private static Dictionary<string, string?> CreateSettings() => new()
    {
        ["REFRESH_COOKIE_NAME"] = "forum_refresh",
        ["REFRESH_COOKIE_SAME_SITE"] = "Lax",
        ["REFRESH_COOKIE_SECURE"] = "false",
        ["CORS_ALLOWED_ORIGINS"] = "http://localhost:3000,https://forum.example.com"
    };
}
