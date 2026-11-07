using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Paye.Core.Authorization;
using Paye.Core.Demo;
using Paye.Infrastructure.Identity;

namespace Paye.Web.Authorization;

public sealed class PermissionAuthorizationHandler(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager) : AuthorizationHandler<PermissionRequirement>
{
    private static readonly HashSet<string> AdminPermissionCodes = CorePermissions.All
        .Select(p => p.Code)
        .ToHashSet(StringComparer.Ordinal);

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userId = userManager.GetUserId(context.User)
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null || !user.IsActive)
        {
            return;
        }

        // Demo Sandbox accounts are strictly isolated: never grant administrative permissions
        if (DemoSandboxConstants.IsDemoUserId(user.Id))
        {
            if (AdminPermissionCodes.Contains(requirement.Permission))
            {
                return;
            }

            var userClaims = await userManager.GetClaimsAsync(user);
            if (userClaims.Any(c =>
                    string.Equals(c.Type, CorePermissions.ClaimType, StringComparison.Ordinal) &&
                    string.Equals(c.Value, requirement.Permission, StringComparison.Ordinal)))
            {
                context.Succeed(requirement);
            }

            return;
        }

        var userRoles = await userManager.GetRolesAsync(user);
        foreach (var roleName in userRoles)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                continue;
            }

            var roleClaims = await roleManager.GetClaimsAsync(role);
            if (roleClaims.Any(c =>
                    string.Equals(c.Type, CorePermissions.ClaimType, StringComparison.Ordinal) &&
                    string.Equals(c.Value, requirement.Permission, StringComparison.Ordinal)))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}
