using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Paye.Core.Auditing;
using Paye.Core.Authorization;
using Paye.Core.Gdpr;
using Paye.Infrastructure.Data;
using Paye.Infrastructure.Identity;

namespace Paye.Infrastructure.Gdpr;

public sealed class GdprUserDataService(
    UserManager<ApplicationUser> userManager,
    PayeDbContext dbContext,
    IAuditLogger auditLogger) : IGdprUserDataService
{
    public async Task<GdprUserExportDto?> ExportUserDataAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);

        var recentAudits = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(a => a.ActorId == user.Id || a.EntityId == user.Id)
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(200)
            .Select(a => new GdprAuditItemDto(
                a.OccurredAtUtc,
                a.Action,
                a.EntityName,
                a.EntityId,
                a.Summary))
            .ToListAsync(cancellationToken);

        await auditLogger.LogCriticalActionAsync(
            action: "GDPR.ExportUserData",
            entityName: nameof(ApplicationUser),
            entityId: user.Id,
            summary: "Exportación RGPD de datos personales del usuario.",
            cancellationToken: cancellationToken);

        return new GdprUserExportDto(
            UserId: user.Id,
            Email: user.Email ?? string.Empty,
            FullName: user.FullName,
            IsActive: user.IsActive,
            TwoFactorEnabled: user.TwoFactorEnabled,
            CreatedAtUtc: user.CreatedAtUtc,
            Roles: roles.ToList(),
            ExportedAtUtc: DateTimeOffset.UtcNow,
            RecentAuditEntries: recentAudits);
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> AnonymizeAndDeactivateUserAsync(
        string userId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(userId, actorUserId, StringComparison.Ordinal))
        {
            return (false, "No puede ejecutar la anonimización RGPD sobre su propia cuenta activa.");
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return (false, "Usuario no encontrado.");
        }

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Contains(SystemRoles.Propietario, StringComparer.Ordinal))
        {
            var owners = await userManager.GetUsersInRoleAsync(SystemRoles.Propietario);
            var activeOwnersCount = owners.Count(o => o.IsActive);
            if (activeOwnersCount <= 1 && user.IsActive)
            {
                return (false, "No se puede anonimizar ni dar de baja al último Propietario activo del sistema.");
            }
        }

        var shortHash = user.Id.Length >= 8 ? user.Id[..8] : user.Id;
        var anonymizedEmail = $"anon-{shortHash}-{Guid.NewGuid().ToString("N")[..8]}@anon.invalid";

        user.FullName = "Usuario Anonimizado RGPD";
        user.Email = anonymizedEmail;
        user.UserName = anonymizedEmail;
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.EmailConfirmed = false;
        user.TwoFactorEnabled = false;
        user.IsActive = false;
        user.PasswordHash = null;

        if (roles.Count > 0)
        {
            await userManager.RemoveFromRolesAsync(user, roles);
        }

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join("; ", updateResult.Errors.Select(e => e.Description));
            return (false, $"No se pudo anonimizar el usuario: {errors}");
        }

        await userManager.UpdateSecurityStampAsync(user);

        // Scrub ActorEmail from historical audit logs for this user while preserving the immutable ActorId trail
        var userAuditEntries = await dbContext.AuditLogs
            .Where(a => a.ActorId == user.Id && a.ActorEmail != null)
            .ToListAsync(cancellationToken);

        foreach (var entry in userAuditEntries)
        {
            entry.ActorEmail = null;
        }

        if (userAuditEntries.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await auditLogger.LogCriticalActionAsync(
            action: "GDPR.AnonymizeUser",
            entityName: nameof(ApplicationUser),
            entityId: user.Id,
            summary: $"Datos personales anonimizados y cuenta desactivada por {actorUserId}.",
            cancellationToken: cancellationToken);

        return (true, null);
    }
}
