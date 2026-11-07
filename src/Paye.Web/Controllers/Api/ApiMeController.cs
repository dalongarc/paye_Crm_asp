using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Paye.Core.Authorization;
using Paye.Core.Navigation;
using Paye.Infrastructure.Identity;

namespace Paye.Web.Controllers.Api;

public sealed record ApiTeamDto(string Name, string LogoName, string Plan);

public sealed record ApiNavItemDto(
    string Title,
    string? Url = null,
    string? IconName = null,
    string? Badge = null,
    IReadOnlyList<ApiNavItemDto>? Items = null);

public sealed record ApiNavGroupDto(
    string Title,
    IReadOnlyList<ApiNavItemDto> Items);

public sealed record ApiNavigationDataDto(
    ApiUserDto User,
    IReadOnlyList<ApiTeamDto> Teams,
    IReadOnlyList<ApiNavGroupDto> NavGroups);

[ApiController]
[Route("api/me")]
[Authorize]
public sealed class ApiMeController(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IEnumerable<IModuleNavigationProvider> navigationProviders,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var user = await GetCurrentApplicationUserAsync();
        if (user is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "No autenticado",
                Detail = "No se encontró la sesión del usuario actual."
            });
        }

        var userDto = await BuildUserDtoAsync(user);
        return Ok(userDto);
    }

    [HttpGet("navigation")]
    [ProducesResponseType<ApiNavigationDataDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNavigation()
    {
        var user = await GetCurrentApplicationUserAsync();
        if (user is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "No autenticado",
                Detail = "No se encontró la sesión del usuario actual."
            });
        }

        var userDto = await BuildUserDtoAsync(user);

        // Recolectar elementos candidatos de los módulos registrados
        var moduleItems = new List<MenuItemDefinition>();
        foreach (var provider in navigationProviders)
        {
            moduleItems.AddRange(provider.GetMenuItems());
        }

        var generalNavItems = new List<ApiNavItemDto>
        {
            new("Dashboard", "/", "LayoutDashboard")
        };

        foreach (var item in moduleItems.OrderBy(i => i.Order))
        {
            if (!string.IsNullOrWhiteSpace(item.RequiredPermission))
            {
                var auth = await authorizationService.AuthorizeAsync(User, item.RequiredPermission);
                if (!auth.Succeeded)
                {
                    continue;
                }
            }

            var url = item.Url == "/facturacion" ? "/facturas" : item.Url;
            var iconName = MapToLucideIcon(item.IconCssClass, item.Title);
            generalNavItems.Add(new ApiNavItemDto(item.Title, url, iconName));
        }

        generalNavItems.Add(new ApiNavItemDto("Tareas y Operaciones", "/tasks", "ListTodo"));

        var adminNavItems = new List<ApiNavItemDto>();

        var userAuth = await authorizationService.AuthorizeAsync(User, CorePermissions.UsuariosVer);
        if (userAuth.Succeeded)
        {
            adminNavItems.Add(new ApiNavItemDto("Usuarios y roles", "/users", "UserCog"));
        }

        adminNavItems.Add(new ApiNavItemDto("Configuración", "/settings", "Settings"));

        var navGroups = new List<ApiNavGroupDto>
        {
            new("General", generalNavItems),
            new("Administración", adminNavItems)
        };

        var teams = new List<ApiTeamDto>
        {
            new("Payé Base", "Command", "Enterprise")
        };

        return Ok(new ApiNavigationDataDto(userDto, teams, navGroups));
    }

    private async Task<ApplicationUser?> GetCurrentApplicationUserAsync()
    {
        var userId = userManager.GetUserId(User)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var user = await userManager.FindByIdAsync(userId);
        return user is { IsActive: true } ? user : null;
    }

    private async Task<ApiUserDto> BuildUserDtoAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var primaryRole = roles.FirstOrDefault() ?? SystemRoles.Empleado;

        var permissionCodes = new HashSet<string>(StringComparer.Ordinal);

        var userClaims = await userManager.GetClaimsAsync(user);
        foreach (var claim in userClaims.Where(c => c.Type == CorePermissions.ClaimType))
        {
            permissionCodes.Add(claim.Value);
        }

        foreach (var roleName in roles)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is not null)
            {
                var roleClaims = await roleManager.GetClaimsAsync(role);
                foreach (var claim in roleClaims.Where(c => c.Type == CorePermissions.ClaimType))
                {
                    permissionCodes.Add(claim.Value);
                }
            }
        }

        return new ApiUserDto(
            Id: user.Id,
            Name: user.FullName,
            Email: user.Email ?? string.Empty,
            Avatar: string.Empty,
            Role: primaryRole,
            Permissions: permissionCodes.OrderBy(p => p).ToList());
    }

    private static string MapToLucideIcon(string iconClass, string title)
    {
        if (iconClass.Contains("Users", StringComparison.OrdinalIgnoreCase) || title.Contains("Clientes", StringComparison.OrdinalIgnoreCase))
            return "Users";
        if (iconClass.Contains("Receipt", StringComparison.OrdinalIgnoreCase) || title.Contains("Factura", StringComparison.OrdinalIgnoreCase))
            return "Receipt";
        if (iconClass.Contains("FileText", StringComparison.OrdinalIgnoreCase) || title.Contains("Notas", StringComparison.OrdinalIgnoreCase))
            return "FileText";
        return "Package";
    }
}
