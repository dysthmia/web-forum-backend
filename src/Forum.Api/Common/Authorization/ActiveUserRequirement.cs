using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Forum.Api.Common.Authorization;

public sealed class ActiveUserRequirement : IAuthorizationRequirement;

public class ActiveUserRequirementHandler(ActiveUserBanService banService)
    : AuthorizationHandler<ActiveUserRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveUserRequirement requirement)
    {
        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdValue, out var userId))
        {
            return;
        }

        if (!await banService.IsBannedAsync(userId))
        {
            context.Succeed(requirement);
        }
    }
}
