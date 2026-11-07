using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paye.Core.Authorization;
using Paye.Core.Configuration;
using Paye.Core.Demo;
using Paye.Infrastructure.Data;
using Paye.Infrastructure.Identity;

namespace Paye.Infrastructure.Demo;

public sealed class DemoSandboxService(
    IOptions<DemoSandboxSettings> demoOptions,
    UserManager<ApplicationUser> userManager,
    PayeDbContext dbContext,
    IEnumerable<IDemoModuleDataProvider> moduleDataProviders,
    ILogger<DemoSandboxService> logger) : IDemoSandboxService
{
    public bool IsEnabled => demoOptions.Value.Enabled;

    public async Task<DemoSessionStartResult> CreateIsolatedDemoSessionAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return new DemoSessionStartResult(
                Succeeded: false,
                ErrorMessage: "El modo demostración no está habilitado en este entorno.");
        }

        await PurgeExpiredDemoSessionsAsync(cancellationToken);

        var demoId = $"{DemoSandboxConstants.DemoUserIdPrefix}{Guid.NewGuid():N}";
        var demoEmail = $"{demoId}{DemoSandboxConstants.DemoEmailDomain}";

        var demoUser = new ApplicationUser
        {
            Id = demoId,
            UserName = demoEmail,
            Email = demoEmail,
            EmailConfirmed = true,
            FullName = "Usuario Demo (Sandbox Aislado)",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var ephemeralPassword = GenerateEphemeralStrongPassword();
        var createResult = await userManager.CreateAsync(demoUser, ephemeralPassword);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            logger.LogError("No se pudo crear la cuenta efímera de demostración: {Errors}", errors);
            return new DemoSessionStartResult(
                Succeeded: false,
                ErrorMessage: "No se pudo inicializar la sesión de demostración.");
        }

        // Grant ONLY isolated module permissions via user claims (never global administrative roles)
        var demoClaims = new List<Claim>
        {
            new(DemoSandboxConstants.ClaimType, DemoSandboxConstants.ClaimValue)
        };

        foreach (var permission in moduleDataProviders.SelectMany(p => p.GetDemoPermissions()).Distinct(StringComparer.Ordinal))
        {
            demoClaims.Add(new Claim(CorePermissions.ClaimType, permission));
        }

        var claimsResult = await userManager.AddClaimsAsync(demoUser, demoClaims);
        if (!claimsResult.Succeeded)
        {
            await userManager.DeleteAsync(demoUser);
            return new DemoSessionStartResult(
                Succeeded: false,
                ErrorMessage: "No se pudieron asignar los permisos aislados de demostración.");
        }

        foreach (var provider in moduleDataProviders)
        {
            await provider.SeedDemoDataAsync(demoId, cancellationToken);
        }

        logger.LogInformation("Sesión de demostración aislada iniciada: {DemoUserId}", demoId);

        return new DemoSessionStartResult(
            Succeeded: true,
            DemoUserId: demoId,
            DemoEmail: demoEmail);
    }

    public async Task CleanupDemoSessionAsync(
        string demoUserId,
        CancellationToken cancellationToken = default)
    {
        if (!DemoSandboxConstants.IsDemoUserId(demoUserId))
        {
            return;
        }

        foreach (var provider in moduleDataProviders)
        {
            await provider.CleanupDemoDataAsync(demoUserId, cancellationToken);
        }

        await dbContext.AuditLogs
            .Where(a => a.ActorId == demoUserId)
            .ExecuteDeleteAsync(cancellationToken);

        var demoUser = await userManager.FindByIdAsync(demoUserId);
        if (demoUser is not null && DemoSandboxConstants.IsDemoUserId(demoUser.Id))
        {
            await userManager.DeleteAsync(demoUser);
        }

        logger.LogInformation("Sesión de demostración aislada eliminada y limpiada: {DemoUserId}", demoUserId);
    }

    private async Task PurgeExpiredDemoSessionsAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-2);
        var expiredDemoUserIds = await userManager.Users
            .AsNoTracking()
            .Where(u => u.Id.StartsWith(DemoSandboxConstants.DemoUserIdPrefix) && u.CreatedAtUtc < cutoff)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        foreach (var expiredId in expiredDemoUserIds)
        {
            await CleanupDemoSessionAsync(expiredId, cancellationToken);
        }
    }

    private static string GenerateEphemeralStrongPassword()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(24);
        return $"Dm!9aZ{Convert.ToBase64String(randomBytes)}#";
    }
}
