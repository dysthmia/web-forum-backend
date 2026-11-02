using System.ComponentModel.DataAnnotations;

namespace Forum.Api.Features.Users;

public record PublicUserProfileResponse(
    long Id,
    string Username,
    string Role,
    DateTime CreatedAt,
    int TopicCount,
    int PostCount);

public record OwnUserProfileResponse(
    long Id,
    string Username,
    string Email,
    string Role,
    DateTime CreatedAt,
    int TopicCount,
    int PostCount);

public class UpdateOwnProfileRequest
{
    [StringLength(32, MinimumLength = 2)]
    [RegularExpression("^[a-zA-Z0-9_]+$")]
    public string? Username { get; init; }

    [EmailAddress]
    [StringLength(254)]
    public string? Email { get; init; }
}
