using Microsoft.AspNetCore.Http;

namespace Forum.Api.Features.Auth;

public class RefreshSessionOptions
{
    public required string CookieName { get; init; }

    public required SameSiteMode CookieSameSite { get; init; }

    public required bool CookieSecure { get; init; }

    public required IReadOnlyList<string> AllowedOrigins { get; init; }
}

public static class RefreshSessionOptionsFactory
{
    public static RefreshSessionOptions Create(IConfiguration configuration)
    {
        var cookieName = Require(configuration, "REFRESH_COOKIE_NAME");
        if (!Enum.TryParse<SameSiteMode>(Require(configuration, "REFRESH_COOKIE_SAME_SITE"), true, out var sameSite)
            || sameSite is not (SameSiteMode.Lax or SameSiteMode.Strict or SameSiteMode.None))
        {
            throw new InvalidOperationException("REFRESH_COOKIE_SAME_SITE must be Lax, Strict, or None.");
        }

        if (!bool.TryParse(Require(configuration, "REFRESH_COOKIE_SECURE"), out var secure))
        {
            throw new InvalidOperationException("REFRESH_COOKIE_SECURE must be true or false.");
        }

        if (sameSite == SameSiteMode.None && !secure)
        {
            throw new InvalidOperationException("REFRESH_COOKIE_SECURE must be true when REFRESH_COOKIE_SAME_SITE is None.");
        }

        var allowedOrigins = Require(configuration, "CORS_ALLOWED_ORIGINS")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(origin => origin.TrimEnd('/'))
            .ToArray();
        if (allowedOrigins.Length == 0 || allowedOrigins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out _)))
        {
            throw new InvalidOperationException("CORS_ALLOWED_ORIGINS must contain one or more absolute origins.");
        }

        return new RefreshSessionOptions
        {
            CookieName = cookieName,
            CookieSameSite = sameSite,
            CookieSecure = secure,
            AllowedOrigins = allowedOrigins
        };
    }

    private static string Require(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{key} is required.");
}
