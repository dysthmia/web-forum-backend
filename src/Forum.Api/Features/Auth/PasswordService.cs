using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Forum.Api.Features.Auth;

public class PasswordService
{
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly string _unknownUserHash;

    public PasswordService(IPasswordHasher<User> passwordHasher)
    {
        _passwordHasher = passwordHasher;
        _unknownUserHash = _passwordHasher.HashPassword(new User(), "invalid-password");
    }

    public string HashPassword(User user, string password) => _passwordHasher.HashPassword(user, password);

    public bool VerifyPassword(User? user, string password)
    {
        var hash = user?.PasswordHash ?? _unknownUserHash;
        var result = _passwordHasher.VerifyHashedPassword(user ?? new User(), hash, password);
        return user is not null && result != PasswordVerificationResult.Failed;
    }
}

public static class PasswordServiceRegistration
{
    public static IServiceCollection AddForumPasswordHashing(this IServiceCollection services, IConfiguration configuration)
    {
        var iterations = configuration.GetValue<int?>("PASSWORD_HASH_ITERATIONS")
            ?? throw new InvalidOperationException("PASSWORD_HASH_ITERATIONS is required.");
        if (iterations < 10_000)
        {
            throw new InvalidOperationException("PASSWORD_HASH_ITERATIONS must be at least 10000.");
        }

        services.Configure<PasswordHasherOptions>(options => options.IterationCount = iterations);
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<PasswordService>();
        return services;
    }
}
