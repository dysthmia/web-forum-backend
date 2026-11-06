using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Forum.Api.Features.Auth;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Forum.Api.Features.Users;

public enum UserProfileFailure
{
    None,
    NotFound,
    DuplicateUser,
    InvalidRequest,
    IncorrectPassword
}

public record UserProfileResult(
    PublicUserProfileResponse? PublicProfile,
    OwnUserProfileResponse? OwnProfile,
    UserProfileFailure Failure)
{
    public static UserProfileResult NotFound() => new(null, null, UserProfileFailure.NotFound);

    public static UserProfileResult Failed(UserProfileFailure failure) => new(null, null, failure);

    public static UserProfileResult Public(PublicUserProfileResponse profile) => new(profile, null, UserProfileFailure.None);

    public static UserProfileResult Own(OwnUserProfileResponse profile) => new(null, profile, UserProfileFailure.None);
}

public class UsersService(ForumDbContext dbContext, PasswordService? passwordService = null)
{
    public async Task<UserProfileResult> GetPublicProfileAsync(long userId, CancellationToken cancellationToken)
    {
        var profile = await GetProfileAsync(userId, cancellationToken);
        return profile is null
            ? UserProfileResult.NotFound()
            : UserProfileResult.Public(ToPublicResponse(profile));
    }

    public async Task<UserProfileResult> GetPublicProfileByUsernameAsync(
        string username,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return UserProfileResult.NotFound();
        }

        var normalizedUsername = username.Trim().ToLowerInvariant();
        var profile = await dbContext.Users.AsNoTracking()
            .Where(user => user.Username.ToLower() == normalizedUsername)
            .Select(user => new UserProfileData(
                user.Id,
                user.Username,
                user.Email,
                user.Role,
                user.CreatedAt,
                dbContext.Topics.Count(topic => topic.AuthorId == user.Id && topic.DeletedAt == null),
                dbContext.Posts.Count(post => post.AuthorId == user.Id && post.DeletedAt == null)))
            .SingleOrDefaultAsync(cancellationToken);

        return profile is null
            ? UserProfileResult.NotFound()
            : UserProfileResult.Public(ToPublicResponse(profile));
    }

    public async Task<UserProfileResult> GetOwnProfileAsync(long userId, CancellationToken cancellationToken)
    {
        var profile = await GetProfileAsync(userId, cancellationToken);
        return profile is null
            ? UserProfileResult.NotFound()
            : UserProfileResult.Own(ToOwnResponse(profile));
    }

    public async Task<UserProfileResult> UpdateOwnProfileAsync(
        long userId,
        UpdateOwnProfileRequest request,
        CancellationToken cancellationToken)
    {
        var username = request.Username?.Trim();
        var email = request.Email?.Trim();
        var newPassword = request.NewPassword;
        var currentPassword = request.CurrentPassword;
        if ((username is not null && string.IsNullOrWhiteSpace(username))
            || (email is not null && string.IsNullOrWhiteSpace(email))
            || (newPassword is not null && string.IsNullOrWhiteSpace(newPassword))
            || (newPassword is null && currentPassword is not null)
            || (username is null && email is null && newPassword is null))
        {
            return UserProfileResult.Failed(UserProfileFailure.InvalidRequest);
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return UserProfileResult.NotFound();
        }

        if (newPassword is not null
            && (string.IsNullOrWhiteSpace(currentPassword)
                || passwordService is null
                || !passwordService.VerifyPassword(user, currentPassword)))
        {
            return UserProfileResult.Failed(UserProfileFailure.IncorrectPassword);
        }

        var normalizedUsername = username?.ToLowerInvariant();
        var normalizedEmail = email?.ToLowerInvariant();
        var duplicateExists = await dbContext.Users.AsNoTracking().AnyAsync(
            item => item.Id != userId
                    && ((normalizedUsername != null && item.Username.ToLower() == normalizedUsername)
                        || (normalizedEmail != null && item.Email.ToLower() == normalizedEmail)),
            cancellationToken);
        if (duplicateExists)
        {
            return UserProfileResult.Failed(UserProfileFailure.DuplicateUser);
        }

        if (username is not null)
        {
            user.Username = username;
        }

        if (email is not null)
        {
            user.Email = email;
        }

        if (newPassword is not null)
        {
            user.PasswordHash = passwordService!.HashPassword(user, newPassword);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return UserProfileResult.Failed(UserProfileFailure.DuplicateUser);
        }

        return await GetOwnProfileAsync(userId, cancellationToken);
    }

    private async Task<UserProfileData?> GetProfileAsync(long userId, CancellationToken cancellationToken) =>
        await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new UserProfileData(
                user.Id,
                user.Username,
                user.Email,
                user.Role,
                user.CreatedAt,
                dbContext.Topics.Count(topic => topic.AuthorId == user.Id && topic.DeletedAt == null),
                dbContext.Posts.Count(post => post.AuthorId == user.Id && post.DeletedAt == null)))
            .SingleOrDefaultAsync(cancellationToken);

    private static PublicUserProfileResponse ToPublicResponse(UserProfileData profile) => new(
        profile.Id,
        profile.Username,
        profile.Role.ToString().ToLowerInvariant(),
        profile.CreatedAt,
        profile.TopicCount,
        profile.PostCount);

    private static OwnUserProfileResponse ToOwnResponse(UserProfileData profile) => new(
        profile.Id,
        profile.Username,
        profile.Email,
        profile.Role.ToString().ToLowerInvariant(),
        profile.CreatedAt,
        profile.TopicCount,
        profile.PostCount);

    private record UserProfileData(
        long Id,
        string Username,
        string Email,
        UserRole Role,
        DateTime CreatedAt,
        int TopicCount,
        int PostCount);
}
