using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Paye.Core.Auditing;
using Paye.Core.Authorization;
using Paye.Core.Demo;
using Paye.Core.Gdpr;
using Paye.Infrastructure.Identity;
using Paye.Web.ViewModels.Admin;

namespace Paye.Web.Controllers;

[Authorize(Policy = CorePermissions.UsuariosVer)]
public sealed class UsersController(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IEnumerable<IPermissionDefinitionProvider> permissionProviders,
    IAuditLogger auditLogger,
    IGdprUserDataService gdprService,
    ILogger<UsersController> logger) : Controller
{
    private static readonly JsonSerializerOptions GdprJsonOptions = new()
    {
        WriteIndented = true
    };

    [HttpGet("/admin/users")]
    public async Task<IActionResult> Index()
    {
        var users = await userManager.Users
            .Where(u => !u.Id.StartsWith(DemoSandboxConstants.DemoUserIdPrefix))
            .OrderBy(u => u.Email)
            .ToListAsync();

        var userItems = new List<UserSummaryItem>(users.Count);
        foreach (var u in users)
        {
            var roles = await userManager.GetRolesAsync(u);
            userItems.Add(new UserSummaryItem(
                u.Id,
                u.Email ?? u.UserName ?? string.Empty,
                u.FullName,
                u.IsActive,
                u.TwoFactorEnabled,
                u.CreatedAtUtc,
                roles.ToList()));
        }

        var availablePermissions = permissionProviders
            .SelectMany(p => p.GetPermissions())
            .DistinctBy(p => p.Code)
            .OrderBy(p => p.Group)
            .ThenBy(p => p.Code)
            .ToList();

        var roleItems = new List<RolePermissionsItem>();
        foreach (var roleName in SystemRoles.All)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            var granted = new HashSet<string>(StringComparer.Ordinal);
            if (role is not null)
            {
                var claims = await roleManager.GetClaimsAsync(role);
                foreach (var claim in claims.Where(c => c.Type == CorePermissions.ClaimType))
                {
                    granted.Add(claim.Value);
                }
            }

            roleItems.Add(new RolePermissionsItem(
                RoleName: roleName,
                IsEditable: !string.Equals(roleName, SystemRoles.Propietario, StringComparison.Ordinal),
                GrantedPermissions: granted));
        }

        return View(new UsersIndexViewModel
        {
            Users = userItems,
            Roles = roleItems,
            AvailablePermissions = availablePermissions
        });
    }

    [Authorize(Policy = CorePermissions.UsuariosGestionar)]
    [HttpGet("/admin/users/create")]
    public IActionResult Create()
    {
        return View(new CreateUserViewModel());
    }

    [Authorize(Policy = CorePermissions.UsuariosGestionar)]
    [HttpPost("/admin/users/create")]
    public async Task<IActionResult> Create(CreateUserViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim();
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "Ya existe un usuario con ese correo electrónico.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = model.FullName.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        await userManager.AddToRoleAsync(user, model.Role);
        logger.LogInformation("Usuario {NewUserId} creado con rol {Role} por {ActorId}.", user.Id, model.Role, userManager.GetUserId(User));

        await auditLogger.LogCriticalActionAsync(
            action: "Users.Create",
            entityName: nameof(ApplicationUser),
            entityId: user.Id,
            summary: $"Alta de usuario con rol {model.Role}.",
            cancellationToken: cancellationToken);

        TempData["SuccessMessage"] = $"Usuario {user.Email} creado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = CorePermissions.UsuariosGestionar)]
    [HttpPost("/admin/users/{id}/toggle-active")]
    public async Task<IActionResult> ToggleActive(string id, CancellationToken cancellationToken)
    {
        var targetUser = await userManager.FindByIdAsync(id);
        if (targetUser is null)
        {
            return NotFound();
        }

        var currentUserId = userManager.GetUserId(User);
        if (string.Equals(targetUser.Id, currentUserId, StringComparison.Ordinal) && targetUser.IsActive)
        {
            TempData["ErrorMessage"] = "No puede desactivar su propia cuenta de usuario.";
            return RedirectToAction(nameof(Index));
        }

        if (targetUser.IsActive && await IsLastActiveOwnerAsync(targetUser))
        {
            TempData["ErrorMessage"] = "No se puede desactivar al último usuario con rol Propietario activo.";
            return RedirectToAction(nameof(Index));
        }

        targetUser.IsActive = !targetUser.IsActive;
        await userManager.UpdateAsync(targetUser);
        await userManager.UpdateSecurityStampAsync(targetUser);

        logger.LogInformation(
            "Estado IsActive de usuario {TargetUserId} cambiado a {IsActive} por {ActorId}.",
            targetUser.Id,
            targetUser.IsActive,
            currentUserId);

        await auditLogger.LogCriticalActionAsync(
            action: targetUser.IsActive ? "Users.Activate" : "Users.Deactivate",
            entityName: nameof(ApplicationUser),
            entityId: targetUser.Id,
            summary: targetUser.IsActive ? "Cuenta de usuario activada." : "Cuenta de usuario desactivada e invalidación de sesiones.",
            cancellationToken: cancellationToken);

        TempData["SuccessMessage"] = targetUser.IsActive
            ? $"Usuario {targetUser.Email} activado."
            : $"Usuario {targetUser.Email} desactivado y sesiones cerradas.";

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = CorePermissions.UsuariosGestionar)]
    [HttpPost("/admin/users/{id}/roles")]
    public async Task<IActionResult> UpdateRole(string id, UpdateUserRoleViewModel model, CancellationToken cancellationToken)
    {
        if (!string.Equals(id, model.UserId, StringComparison.Ordinal) || !ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Datos de cambio de rol no válidos.";
            return RedirectToAction(nameof(Index));
        }

        var targetUser = await userManager.FindByIdAsync(id);
        if (targetUser is null)
        {
            return NotFound();
        }

        var currentRoles = await userManager.GetRolesAsync(targetUser);
        var isCurrentlyOwner = currentRoles.Contains(SystemRoles.Propietario, StringComparer.Ordinal);
        var willBeOwner = string.Equals(model.Role, SystemRoles.Propietario, StringComparison.Ordinal);

        if (isCurrentlyOwner && !willBeOwner && await IsLastActiveOwnerAsync(targetUser))
        {
            TempData["ErrorMessage"] = "No se puede quitar el último rol de Propietario activo del sistema.";
            return RedirectToAction(nameof(Index));
        }

        if (currentRoles.Count > 0)
        {
            await userManager.RemoveFromRolesAsync(targetUser, currentRoles);
        }

        await userManager.AddToRoleAsync(targetUser, model.Role);
        await userManager.UpdateSecurityStampAsync(targetUser);

        logger.LogInformation(
            "Rol de usuario {TargetUserId} actualizado a {NewRole} por {ActorId}.",
            targetUser.Id,
            model.Role,
            userManager.GetUserId(User));

        await auditLogger.LogCriticalActionAsync(
            action: "Users.UpdateRole",
            entityName: nameof(ApplicationUser),
            entityId: targetUser.Id,
            summary: $"Cambio de rol a {model.Role}.",
            cancellationToken: cancellationToken);

        TempData["SuccessMessage"] = $"Rol de {targetUser.Email} actualizado a {model.Role}.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = CorePermissions.RolesGestionar)]
    [HttpPost("/admin/roles/permissions")]
    public async Task<IActionResult> UpdateRolePermissions(UpdateRolePermissionsViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "No se pudieron actualizar los permisos del rol.";
            return RedirectToAction(nameof(Index));
        }

        var role = await roleManager.FindByNameAsync(model.RoleName);
        if (role is null)
        {
            return NotFound();
        }

        var validPermissionCodes = permissionProviders
            .SelectMany(p => p.GetPermissions())
            .Select(p => p.Code)
            .ToHashSet(StringComparer.Ordinal);

        var requestedPermissions = (model.SelectedPermissions ?? [])
            .Where(validPermissionCodes.Contains)
            .ToHashSet(StringComparer.Ordinal);

        var currentClaims = await roleManager.GetClaimsAsync(role);
        foreach (var existingClaim in currentClaims.Where(c => c.Type == CorePermissions.ClaimType))
        {
            if (!requestedPermissions.Contains(existingClaim.Value))
            {
                await roleManager.RemoveClaimAsync(role, existingClaim);
            }
        }

        var remainingValues = (await roleManager.GetClaimsAsync(role))
            .Where(c => c.Type == CorePermissions.ClaimType)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var perm in requestedPermissions)
        {
            if (!remainingValues.Contains(perm))
            {
                await roleManager.AddClaimAsync(role, new Claim(CorePermissions.ClaimType, perm));
            }
        }

        logger.LogInformation(
            "Permisos del rol {RoleName} actualizados ({Count} permisos) por {ActorId}.",
            role.Name,
            requestedPermissions.Count,
            userManager.GetUserId(User));

        await auditLogger.LogCriticalActionAsync(
            action: "Roles.UpdatePermissions",
            entityName: nameof(IdentityRole),
            entityId: role.Name,
            summary: $"Actualización de permisos del rol {role.Name} ({requestedPermissions.Count} permisos activos).",
            cancellationToken: cancellationToken);

        TempData["SuccessMessage"] = $"Permisos del rol {role.Name} actualizados correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = CorePermissions.UsuariosGestionar)]
    [HttpGet("/admin/users/{id}/export-gdpr")]
    public async Task<IActionResult> ExportGdpr(string id, CancellationToken cancellationToken)
    {
        var exportDto = await gdprService.ExportUserDataAsync(id, cancellationToken);
        if (exportDto is null)
        {
            return NotFound();
        }

        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(exportDto, GdprJsonOptions));
        var fileName = $"rgpd-usuario-{id[..Math.Min(8, id.Length)]}.json";
        return File(jsonBytes, "application/json; charset=utf-8", fileName);
    }

    [Authorize(Policy = CorePermissions.UsuariosGestionar)]
    [HttpPost("/admin/users/{id}/anonymize-gdpr")]
    public async Task<IActionResult> AnonymizeGdpr(string id, CancellationToken cancellationToken)
    {
        var actorId = userManager.GetUserId(User) ?? "system";
        var (succeeded, errorMessage) = await gdprService.AnonymizeAndDeactivateUserAsync(id, actorId, cancellationToken);

        if (!succeeded)
        {
            TempData["ErrorMessage"] = errorMessage ?? "No se pudo anonimizar el usuario.";
        }
        else
        {
            TempData["SuccessMessage"] = "Datos personales del usuario anonimizados y cuenta dada de baja según RGPD.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> IsLastActiveOwnerAsync(ApplicationUser targetUser)
    {
        var owners = await userManager.GetUsersInRoleAsync(SystemRoles.Propietario);
        var activeOwnersCount = owners.Count(u => u.IsActive);

        return activeOwnersCount <= 1 &&
               owners.Any(u => u.IsActive && string.Equals(u.Id, targetUser.Id, StringComparison.Ordinal));
    }
}
