using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Paye.Core.Auditing;
using Paye.Core.Authorization;
using Paye.Core.Demo;
using Paye.Infrastructure.Identity;

namespace Paye.Web.Controllers.Api;

public sealed record UserApiDto(
    string Id,
    string FirstName,
    string LastName,
    string Username,
    string Email,
    string PhoneNumber,
    string Status,
    string Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateUserApiRequest(
    [Required] string FirstName,
    [Required] string LastName,
    [Required] string Username,
    [Required, EmailAddress] string Email,
    string? PhoneNumber,
    [Required] string Password,
    string? Role);

public sealed record UpdateUserApiRequest(
    [Required] string FirstName,
    [Required] string LastName,
    [Required] string Username,
    [Required, EmailAddress] string Email,
    string? PhoneNumber,
    string? Password,
    string? Status,
    string? Role);

[ApiController]
[Route("api/users")]
[Authorize(Policy = CorePermissions.UsuariosVer)]
public sealed class ApiUsersController(
    UserManager<ApplicationUser> userManager,
    IAuditLogger auditLogger,
    ILogger<ApiUsersController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserApiDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var users = await userManager.Users
            .Where(u => !u.Id.StartsWith(DemoSandboxConstants.DemoUserIdPrefix))
            .OrderBy(u => u.Email)
            .ToListAsync(cancellationToken);

        var list = new List<UserApiDto>(users.Count);
        foreach (var u in users)
        {
            var parts = (u.FullName ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var firstName = parts.Length > 0 ? parts[0] : (u.UserName ?? "Usuario");
            var lastName = parts.Length > 1 ? parts[1] : string.Empty;

            var roles = await userManager.GetRolesAsync(u);
            var roleLabel = roles.Contains(SystemRoles.Propietario, StringComparer.OrdinalIgnoreCase)
                ? "admin"
                : roles.FirstOrDefault()?.ToLowerInvariant() ?? "admin";

            list.Add(new UserApiDto(
                Id: u.Id,
                FirstName: firstName,
                LastName: lastName,
                Username: u.UserName ?? u.Email ?? u.Id,
                Email: u.Email ?? string.Empty,
                PhoneNumber: u.PhoneNumber ?? string.Empty,
                Status: u.IsActive ? "active" : "inactive",
                Role: "admin",
                CreatedAt: u.CreatedAtUtc,
                UpdatedAt: u.CreatedAtUtc));
        }

        return Ok(list);
    }

    [HttpPost]
    [Authorize(Policy = CorePermissions.UsuariosGestionar)]
    [ProducesResponseType<UserApiDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserApiRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Correo en uso",
                Detail = "Ya existe un usuario con ese correo electrónico."
            });
        }

        var username = request.Username.Trim();
        var existingUserByName = await userManager.FindByNameAsync(username);
        if (existingUserByName is not null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Nombre de usuario en uso",
                Detail = "Ya existe un usuario con ese nombre de usuario."
            });
        }

        var fullName = $"{request.FirstName.Trim()} {request.LastName.Trim()}".Trim();
        var user = new ApplicationUser
        {
            UserName = username,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var firstError = result.Errors.FirstOrDefault()?.Description ?? "Error al crear el usuario en Identity.";
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error de validación",
                Detail = firstError
            });
        }

        // Asignar rol Propietario (Administrador)
        await userManager.AddToRoleAsync(user, SystemRoles.Propietario);

        logger.LogInformation("Usuario {NewUserId} creado vía API por {ActorId}.", user.Id, userManager.GetUserId(User));

        await auditLogger.LogCriticalActionAsync(
            action: "Users.Create",
            entityName: nameof(ApplicationUser),
            entityId: user.Id,
            summary: $"Alta de usuario {user.Email} con rol {SystemRoles.Propietario}.",
            cancellationToken: cancellationToken);

        var responseDto = new UserApiDto(
            Id: user.Id,
            FirstName: request.FirstName.Trim(),
            LastName: request.LastName.Trim(),
            Username: user.UserName,
            Email: user.Email,
            PhoneNumber: user.PhoneNumber ?? string.Empty,
            Status: "active",
            Role: "admin",
            CreatedAt: user.CreatedAtUtc,
            UpdatedAt: user.CreatedAtUtc);

        return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, responseDto);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = CorePermissions.UsuariosGestionar)]
    [ProducesResponseType<UserApiDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserApiRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var newEmail = request.Email.Trim().ToLowerInvariant();
        if (!string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            var existingByEmail = await userManager.FindByEmailAsync(newEmail);
            if (existingByEmail is not null && !string.Equals(existingByEmail.Id, user.Id, StringComparison.Ordinal))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Correo en uso",
                    Detail = "Ya existe otro usuario con ese correo electrónico."
                });
            }

            user.Email = newEmail;
        }

        var newUsername = request.Username.Trim();
        if (!string.Equals(user.UserName, newUsername, StringComparison.OrdinalIgnoreCase))
        {
            var existingByName = await userManager.FindByNameAsync(newUsername);
            if (existingByName is not null && !string.Equals(existingByName.Id, user.Id, StringComparison.Ordinal))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Nombre de usuario en uso",
                    Detail = "Ya existe otro usuario con ese nombre de usuario."
                });
            }

            user.UserName = newUsername;
        }

        user.FullName = $"{request.FirstName.Trim()} {request.LastName.Trim()}".Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var wantsActive = string.Equals(request.Status, "active", StringComparison.OrdinalIgnoreCase);
            if (!wantsActive && user.IsActive && await IsLastActiveOwnerAsync(user))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Operación no permitida",
                    Detail = "No se puede desactivar al único usuario Propietario activo del sistema."
                });
            }

            user.IsActive = wantsActive;
        }

        // Cambio de contraseña si se especificó
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var passResult = await userManager.ResetPasswordAsync(user, resetToken, request.Password);
            if (!passResult.Succeeded)
            {
                var passError = passResult.Errors.FirstOrDefault()?.Description ?? "Error al actualizar la contraseña.";
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Error en contraseña",
                    Detail = passError
                });
            }

            await userManager.UpdateSecurityStampAsync(user);
        }

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var error = updateResult.Errors.FirstOrDefault()?.Description ?? "Error al actualizar el usuario.";
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error al actualizar",
                Detail = error
            });
        }

        await auditLogger.LogCriticalActionAsync(
            action: "Users.Update",
            entityName: nameof(ApplicationUser),
            entityId: user.Id,
            summary: $"Modificación de usuario {user.Email}.",
            cancellationToken: cancellationToken);

        var responseDto = new UserApiDto(
            Id: user.Id,
            FirstName: request.FirstName.Trim(),
            LastName: request.LastName.Trim(),
            Username: user.UserName ?? user.Email ?? string.Empty,
            Email: user.Email ?? string.Empty,
            PhoneNumber: user.PhoneNumber ?? string.Empty,
            Status: user.IsActive ? "active" : "inactive",
            Role: "admin",
            CreatedAt: user.CreatedAtUtc,
            UpdatedAt: DateTimeOffset.UtcNow);

        return Ok(responseDto);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = CorePermissions.UsuariosGestionar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(string id, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (await IsLastActiveOwnerAsync(user))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Operación no permitida",
                Detail = "No se puede eliminar al único usuario Propietario activo del sistema."
            });
        }

        var currentUserId = userManager.GetUserId(User);
        if (string.Equals(user.Id, currentUserId, StringComparison.Ordinal))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Operación no permitida",
                Detail = "No puede eliminar su propia cuenta de usuario en sesión."
            });
        }

        var deleteResult = await userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            var error = deleteResult.Errors.FirstOrDefault()?.Description ?? "Error al eliminar el usuario.";
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error al eliminar",
                Detail = error
            });
        }

        logger.LogInformation("Usuario {DeletedUserId} ({Email}) eliminado por {ActorId}.", user.Id, user.Email, currentUserId);

        await auditLogger.LogCriticalActionAsync(
            action: "Users.Delete",
            entityName: nameof(ApplicationUser),
            entityId: user.Id,
            summary: $"Eliminación permanente de usuario {user.Email}.",
            cancellationToken: cancellationToken);

        return NoContent();
    }

    private async Task<bool> IsLastActiveOwnerAsync(ApplicationUser targetUser)
    {
        var owners = await userManager.GetUsersInRoleAsync(SystemRoles.Propietario);
        var activeOwnersCount = owners.Count(u => u.IsActive);

        return activeOwnersCount <= 1 &&
               owners.Any(u => u.IsActive && string.Equals(u.Id, targetUser.Id, StringComparison.Ordinal));
    }
}
