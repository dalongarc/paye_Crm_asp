using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Paye.Core.Authorization;

namespace Paye.Infrastructure.Identity;

public sealed class IdentitySeeder(
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IEnumerable<IPermissionDefinitionProvider> permissionProviders,
    IConfiguration configuration,
    ILogger<IdentitySeeder> logger)
{
    public async Task SeedRolesAndPermissionsAsync()
    {
        var allPermissions = permissionProviders
            .SelectMany(p => p.GetPermissions())
            .DistinctBy(p => p.Code)
            .ToList();

        foreach (var roleName in SystemRoles.All)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            var isNewRole = false;

            if (role is null)
            {
                role = new IdentityRole(roleName);
                var createResult = await roleManager.CreateAsync(role);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"No se pudo crear el rol base '{roleName}': {errors}");
                }
                isNewRole = true;
            }

            // Ensure Propietario always has every registered permission, and seed default permissions for newly created roles
            if (isNewRole || string.Equals(roleName, SystemRoles.Propietario, StringComparison.Ordinal))
            {
                var existingClaims = await roleManager.GetClaimsAsync(role);
                var existingPermissionCodes = existingClaims
                    .Where(c => c.Type == CorePermissions.ClaimType)
                    .Select(c => c.Value)
                    .ToHashSet(StringComparer.Ordinal);

                var targetPermissions = string.Equals(roleName, SystemRoles.Propietario, StringComparison.Ordinal)
                    ? allPermissions
                    : allPermissions.Where(p => p.DefaultRoles.Contains(roleName, StringComparer.Ordinal));

                foreach (var perm in targetPermissions)
                {
                    if (!existingPermissionCodes.Contains(perm.Code))
                    {
                        await roleManager.AddClaimAsync(role, new Claim(CorePermissions.ClaimType, perm.Code));
                    }
                }
            }
        }
    }

    public async Task SeedInitialOwnerFromEnvironmentAsync()
    {
        var ownerEmail = configuration["SEED_OWNER_EMAIL"]?.Trim();
        var ownerPassword = configuration["SEED_OWNER_PASSWORD"];
        var ownerFullName = configuration["SEED_OWNER_FULLNAME"]?.Trim();

        // Si no se especificó email, usamos el predeterminado para comandos de semilla local/cli
        if (string.IsNullOrWhiteSpace(ownerEmail))
        {
            ownerEmail = "propietario@payedigital.local";
        }

        var existingOwners = await userManager.GetUsersInRoleAsync(SystemRoles.Propietario);
        if (existingOwners.Count > 0)
        {
            logger.LogWarning(
                "SEGURIDAD: Ya existe al menos un usuario con rol Propietario. Ignorando siembra inicial.");
            return;
        }

        var password = ownerPassword;
        var wasGenerated = false;

        if (string.IsNullOrWhiteSpace(password))
        {
            password = GenerateSecurePassword(20);
            wasGenerated = true;
        }
        if (existingOwners.Count > 0)
        {
            logger.LogWarning(
                "SEGURIDAD: Ya existe al menos un usuario con rol Propietario. Ignorando SEED_OWNER_EMAIL. " +
                "Elimina las variables SEED_OWNER_EMAIL y SEED_OWNER_PASSWORD de la configuración del entorno.");
            return;
        }

        var user = new ApplicationUser
        {
            UserName = ownerEmail,
            Email = ownerEmail,
            EmailConfirmed = true,
            FullName = string.IsNullOrWhiteSpace(ownerFullName) ? "Propietario" : ownerFullName,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(
                $"No se pudo sembrar el usuario Propietario inicial (verifica que la contraseña tenga al menos 12 caracteres, mayúsculas, minúsculas, número y símbolo): {errors}");
        }

        await userManager.AddToRoleAsync(user, SystemRoles.Propietario);

        if (wasGenerated)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
            Console.WriteLine("========================================================================");
            Console.WriteLine("[SEGURIDAD] CREDENCIALES INICIALES DEL PROPIETARIO (SEMILLA GENERADA):");
            Console.WriteLine($"Usuario:    {ownerEmail}");
            Console.WriteLine($"Contraseña: {password}");
            Console.WriteLine("Guarda esta contraseña de forma segura. Esta es la ÚNICA vez que se mostrará.");
            Console.WriteLine("========================================================================");
            Console.WriteLine();
            Console.ResetColor();
        }

        logger.LogWarning(
            "SEGURIDAD: Primer usuario Propietario sembrado correctamente ({Email}).",
            ownerEmail);
    }

    private static string GenerateSecurePassword(int length = 20)
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string specials = "!@#$%^&*()-_=+";
        const string all = upper + lower + digits + specials;

        var chars = new char[length];
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        chars[3] = specials[RandomNumberGenerator.GetInt32(specials.Length)];

        for (var i = 4; i < length; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }
}
