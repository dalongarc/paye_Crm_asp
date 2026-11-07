using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Paye.Core.Authorization;
using Paye.Infrastructure.Data;
using Paye.Infrastructure.Identity;
using Paye.Tests.Infrastructure;
using Testcontainers.PostgreSql;

namespace Paye.Tests.Milestone2;

public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("paye_test_m2")
        .WithUsername("paye_test_user")
        .WithPassword("TestOnly_NotProd_987!")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        TestDatabaseGuard.EnsureIsolatedTestDatabase(ConnectionString);

        var optionsBuilder = new DbContextOptionsBuilder<PayeDbContext>();
        optionsBuilder.UseNpgsql(ConnectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(typeof(PayeDbContext).Assembly.FullName);
        });

        await using var db = new PayeDbContext(optionsBuilder.Options);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

public sealed class IdentityAndAuthorizationTests(PostgreSqlContainerFixture dbFixture)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private const string InitialOwnerEmail = "propietario.test@payedigital.test";
    private const string InitialOwnerPassword = "ClaveSegura#Paye2026!";

    private async Task<PayeWebFactory> CreateInitializedFactoryAsync(
        IDictionary<string, string?>? extraOverrides = null)
    {
        var overrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = dbFixture.ConnectionString,
            ["SEED_OWNER_EMAIL"] = InitialOwnerEmail,
            ["SEED_OWNER_PASSWORD"] = InitialOwnerPassword,
            ["SEED_OWNER_FULLNAME"] = "Propietario Pruebas"
        };

        if (extraOverrides is not null)
        {
            foreach (var kvp in extraOverrides)
            {
                overrides[kvp.Key] = kvp.Value;
            }
        }

        var factory = new PayeWebFactory(overrides);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();
        await db.Database.MigrateAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await seeder.SeedRolesAndPermissionsAsync();
        await seeder.SeedInitialOwnerFromEnvironmentAsync();

        return factory;
    }

    [Fact]
    public async Task InitialIdentity_MigrationAppliesToPostgreSql_AndSeedsRolesPermissionsAndOwnerIdempotently()
    {
        using var factory = await CreateInitializedFactoryAsync();
        using var scope = factory.Services.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();

        foreach (var roleName in SystemRoles.All)
        {
            Assert.True(await roleManager.RoleExistsAsync(roleName), $"El rol '{roleName}' debe existir.");
        }

        var ownerRole = await roleManager.FindByNameAsync(SystemRoles.Propietario);
        Assert.NotNull(ownerRole);
        var ownerClaims = await roleManager.GetClaimsAsync(ownerRole);
        Assert.Contains(ownerClaims, c => c.Type == CorePermissions.ClaimType && c.Value == CorePermissions.UsuariosGestionar);
        Assert.Contains(ownerClaims, c => c.Type == CorePermissions.ClaimType && c.Value == CorePermissions.RolesGestionar);

        var ownerUser = await userManager.FindByEmailAsync(InitialOwnerEmail);
        Assert.NotNull(ownerUser);
        Assert.True(ownerUser.IsActive);
        Assert.True(await userManager.IsInRoleAsync(ownerUser, SystemRoles.Propietario));

        // Second execution must be idempotent and not fail or create duplicates
        await seeder.SeedRolesAndPermissionsAsync();
        await seeder.SeedInitialOwnerFromEnvironmentAsync();

        var allOwners = await userManager.GetUsersInRoleAsync(SystemRoles.Propietario);
        Assert.Single(allOwners, u => string.Equals(u.Email, InitialOwnerEmail, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_SucceedsWithValidCredentials_IssuesSecureHttpOnlyCookie_AndAntiforgeryBlocksForgedPost()
    {
        using var factory = await CreateInitializedFactoryAsync();
        using var session = new TestBrowserSession(factory);

        // 1. Forged POST without Antiforgery token must return 400 Bad Request
        using var forgedResponse = await session.PostFormWithoutAntiforgeryAsync("/account/login", new Dictionary<string, string>
        {
            ["Email"] = InitialOwnerEmail,
            ["Password"] = InitialOwnerPassword
        });
        Assert.Equal(HttpStatusCode.BadRequest, forgedResponse.StatusCode);

        // 2. Valid login with Antiforgery token succeeds and sets __Host-Paye.Auth cookie
        using var loginResponse = await session.LoginAsync(InitialOwnerEmail, InitialOwnerPassword);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.Equal("/", loginResponse.Headers.Location?.OriginalString);

        Assert.True(loginResponse.Headers.TryGetValues("Set-Cookie", out var setCookies));
        var authCookieHeader = setCookies.FirstOrDefault(c => c.StartsWith("__Host-Paye.Auth=", StringComparison.Ordinal));
        Assert.NotNull(authCookieHeader);
        Assert.Contains("httponly", authCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", authCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", authCookieHeader, StringComparison.OrdinalIgnoreCase);

        // 3. Authenticated GET / succeeds and preserves strict CSP without unsafe-inline
        using var homeResponse = await session.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);
        AssertStrictCspHeader(homeResponse);
    }

    [Fact]
    public async Task Login_LocksOutAccount_AfterFiveFailedPasswordAttempts()
    {
        using var factory = await CreateInitializedFactoryAsync();
        var lockTargetEmail = $"lockout.{Guid.NewGuid():N}@payedigital.test";
        await CreateUserDirectAsync(factory, lockTargetEmail, "ClaveValida#2026!", SystemRoles.Empleado);

        using var session = new TestBrowserSession(factory);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var failedResp = await session.LoginAsync(lockTargetEmail, "ClaveIncorrecta#0000!");
            Assert.Equal(HttpStatusCode.OK, failedResp.StatusCode);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var lockedUser = await userManager.FindByEmailAsync(lockTargetEmail);
            Assert.NotNull(lockedUser);
            Assert.True(await userManager.IsLockedOutAsync(lockedUser));
        }

        // Even with the correct password on the 6th attempt, login is rejected due to lockout
        using var sixthResponse = await session.LoginAsync(lockTargetEmail, "ClaveValida#2026!");
        var html = await sixthResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, sixthResponse.StatusCode);
        Assert.Contains("bloqueada temporalmente", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeactivatingUser_PreventsLogin_AndImmediatelyInvalidatesExistingActiveSession()
    {
        using var factory = await CreateInitializedFactoryAsync();
        var employeeEmail = $"empleado.activo.{Guid.NewGuid():N}@payedigital.test";
        const string employeePassword = "ClaveEmpleado#2026!";
        var employeeId = await CreateUserDirectAsync(factory, employeeEmail, employeePassword, SystemRoles.Empleado);

        using var employeeSession = new TestBrowserSession(factory);
        using var loginResp = await employeeSession.LoginAsync(employeeEmail, employeePassword);
        Assert.Equal(HttpStatusCode.Redirect, loginResp.StatusCode);

        using var initialHomeResp = await employeeSession.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, initialHomeResp.StatusCode);

        // Owner logs in on another session and deactivates the employee via /admin/users/toggle-active
        using var ownerSession = new TestBrowserSession(factory);
        await ownerSession.LoginAsync(InitialOwnerEmail, InitialOwnerPassword);

        using var toggleResp = await ownerSession.PostFormWithAntiforgeryAsync(
            "/admin/users",
            $"/admin/users/{employeeId}/toggle-active",
            new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, toggleResp.StatusCode);

        // Immediately on the next request, the employee's open session is invalidated by SecurityStampValidator + IsActive check
        using var afterDeactivationResp = await employeeSession.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, afterDeactivationResp.StatusCode);
        Assert.StartsWith("/account/login", afterDeactivationResp.Headers.Location?.PathAndQuery ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        // Deactivated employee cannot log in again
        using var freshEmployeeSession = new TestBrowserSession(factory);
        using var reloginResp = await freshEmployeeSession.LoginAsync(employeeEmail, employeePassword);
        Assert.Equal(HttpStatusCode.OK, reloginResp.StatusCode);
    }

    [Fact]
    public async Task Permissions_DenyAccessToUnauthorizedRole_AndDynamicallyGrantAccessWhenRoleClaimAdded()
    {
        using var factory = await CreateInitializedFactoryAsync();
        var employeeEmail = $"permisos.{Guid.NewGuid():N}@payedigital.test";
        const string employeePassword = "ClaveEmpleado#2026!";
        await CreateUserDirectAsync(factory, employeeEmail, employeePassword, SystemRoles.Empleado);

        using var employeeSession = new TestBrowserSession(factory);
        await employeeSession.LoginAsync(employeeEmail, employeePassword);

        // By default, Empleado does NOT have Usuarios.Ver -> redirected to AccessDenied
        using var deniedGetUsers = await employeeSession.GetAsync("/admin/users");
        Assert.Equal(HttpStatusCode.Redirect, deniedGetUsers.StatusCode);
        Assert.StartsWith("/account/access-denied", deniedGetUsers.Headers.Location?.PathAndQuery ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        // Owner grants Usuarios.Ver (but NOT Usuarios.Gestionar) to Empleado role via /admin/roles/permissions
        using var ownerSession = new TestBrowserSession(factory);
        await ownerSession.LoginAsync(InitialOwnerEmail, InitialOwnerPassword);

        using var grantResp = await ownerSession.PostFormWithAntiforgeryAsync(
            "/admin/users",
            "/admin/roles/permissions",
            new Dictionary<string, string>
            {
                ["RoleName"] = SystemRoles.Empleado,
                ["SelectedPermissions"] = CorePermissions.UsuariosVer
            });
        Assert.Equal(HttpStatusCode.Redirect, grantResp.StatusCode);

        // Re-login employee so role claims in principal are refreshed
        using var refreshedEmployeeSession = new TestBrowserSession(factory);
        await refreshedEmployeeSession.LoginAsync(employeeEmail, employeePassword);

        using var allowedGetUsers = await refreshedEmployeeSession.GetAsync("/admin/users");
        Assert.Equal(HttpStatusCode.OK, allowedGetUsers.StatusCode);
        AssertStrictCspHeader(allowedGetUsers);

        // Creating a user still requires Usuarios.Gestionar -> denied
        using var deniedCreateUser = await refreshedEmployeeSession.PostFormWithAntiforgeryAsync(
            "/admin/users",
            "/admin/users/create",
            new Dictionary<string, string>
            {
                ["FullName"] = "Intento No Autorizado",
                ["Email"] = $"noauth.{Guid.NewGuid():N}@payedigital.test",
                ["Password"] = "ClaveSegura#2026!",
                ["Role"] = SystemRoles.Empleado
            });
        Assert.Equal(HttpStatusCode.Redirect, deniedCreateUser.StatusCode);
        Assert.StartsWith("/account/access-denied", deniedCreateUser.Headers.Location?.PathAndQuery ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LastOwnerProtection_PreventsSoleOwnerFromDeactivatingSelf_OrRemovingOwnOwnerRole()
    {
        using var factory = await CreateInitializedFactoryAsync();
        string ownerId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var owners = await userManager.GetUsersInRoleAsync(SystemRoles.Propietario);
            var initialOwner = owners.First(u => string.Equals(u.Email, InitialOwnerEmail, StringComparison.OrdinalIgnoreCase));
            ownerId = initialOwner.Id;
        }

        using var ownerSession = new TestBrowserSession(factory);
        await ownerSession.LoginAsync(InitialOwnerEmail, InitialOwnerPassword);

        // 1. Sole owner tries to deactivate self
        using var selfDeactivateResp = await ownerSession.PostFormWithAntiforgeryAsync(
            "/admin/users",
            $"/admin/users/{ownerId}/toggle-active",
            new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, selfDeactivateResp.StatusCode);

        // 2. Sole owner tries to demote self to Empleado
        using var selfDemoteResp = await ownerSession.PostFormWithAntiforgeryAsync(
            "/admin/users",
            $"/admin/users/{ownerId}/roles",
            new Dictionary<string, string>
            {
                ["UserId"] = ownerId,
                ["Role"] = SystemRoles.Empleado
            });
        Assert.Equal(HttpStatusCode.Redirect, selfDemoteResp.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var ownerAfter = await userManager.FindByIdAsync(ownerId);
            Assert.NotNull(ownerAfter);
            Assert.True(ownerAfter.IsActive);
            Assert.True(await userManager.IsInRoleAsync(ownerAfter, SystemRoles.Propietario));
        }
    }

    [Fact]
    public async Task ForgotPassword_DoesNotSimulateSending_AndShowsExplicitUnconfiguredNotice_WhenSmtpMissing()
    {
        using var factory = await CreateInitializedFactoryAsync();
        using var session = new TestBrowserSession(factory);

        using var forgotResp = await session.PostFormWithAntiforgeryAsync(
            "/account/forgot-password",
            "/account/forgot-password",
            new Dictionary<string, string> { ["Email"] = InitialOwnerEmail });

        var html = await forgotResp.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, forgotResp.StatusCode);
        Assert.Contains("El envío de correo (SMTP) no está configurado", html, StringComparison.Ordinal);
        AssertStrictCspHeader(forgotResp);
    }

    [Fact]
    public async Task TwoFactorSetup_EnablesTotpForUser_WithValidVerificationCode()
    {
        using var factory = await CreateInitializedFactoryAsync();
        var userEmail = $"2fa.user.{Guid.NewGuid():N}@payedigital.test";
        const string userPassword = "Clave2FA#Paye2026!";
        var userId = await CreateUserDirectAsync(factory, userEmail, userPassword, SystemRoles.Encargado);

        using var session = new TestBrowserSession(factory);
        await session.LoginAsync(userEmail, userPassword);

        using var setupPage = await session.GetAsync("/account/two-factor");
        Assert.Equal(HttpStatusCode.OK, setupPage.StatusCode);
        AssertStrictCspHeader(setupPage);

        string unformattedKey;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);
            Assert.NotNull(user);
            unformattedKey = (await userManager.GetAuthenticatorKeyAsync(user))!;
            Assert.False(string.IsNullOrWhiteSpace(unformattedKey));
        }

        var totpCode = ComputeTotpCode(unformattedKey, DateTimeOffset.UtcNow);

        using var enableResp = await session.PostFormWithAntiforgeryAsync(
            "/account/two-factor",
            "/account/two-factor/enable",
            new Dictionary<string, string> { ["VerificationCode"] = totpCode });
        Assert.Equal(HttpStatusCode.Redirect, enableResp.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);
            Assert.NotNull(user);
            Assert.True(await userManager.GetTwoFactorEnabledAsync(user));
        }
    }

    private static async Task<string> CreateUserDirectAsync(
        PayeWebFactory factory,
        string email,
        string password,
        string roleName)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = "Usuario Prueba",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, password);
        Assert.True(createResult.Succeeded);

        var roleResult = await userManager.AddToRoleAsync(user, roleName);
        Assert.True(roleResult.Succeeded);

        return user.Id;
    }

    private static void AssertStrictCspHeader(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var cspValues));
        var csp = string.Join(" ", cspValues);
        Assert.DoesNotContain("unsafe-inline", csp, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unsafe-eval", csp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
    }

    private static string ComputeTotpCode(string base32Secret, DateTimeOffset timestamp)
    {
        var keyBytes = Base32Decode(base32Secret);
        var counter = timestamp.ToUnixTimeSeconds() / 30;
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counterBytes);
        }

#pragma warning disable CA5350 // RFC 6238 TOTP standard uses HMAC-SHA1
        using var hmac = new HMACSHA1(keyBytes);
#pragma warning restore CA5350
        var hash = hmac.ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var otp = binaryCode % 1_000_000;
        return otp.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var cleaned = input.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        var output = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var ch in cleaned)
        {
            var val = alphabet.IndexOf(ch, StringComparison.Ordinal);
            if (val < 0)
            {
                continue;
            }

            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }

        return [.. output];
    }

    private sealed class TestBrowserSession : IDisposable
    {
        private static readonly Regex AntiforgeryTokenRegex = new(
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly HttpClient _client;
        private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

        public TestBrowserSession(PayeWebFactory factory)
        {
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = false
            });
        }

        public async Task<HttpResponseMessage> GetAsync(string path)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            AttachCookies(request);
            var response = await _client.SendAsync(request);
            CaptureCookies(response);
            return response;
        }

        public async Task<HttpResponseMessage> LoginAsync(string email, string password)
        {
            return await PostFormWithAntiforgeryAsync(
                "/account/login",
                "/account/login",
                new Dictionary<string, string>
                {
                    ["Email"] = email,
                    ["Password"] = password
                });
        }

        public async Task<HttpResponseMessage> PostFormWithAntiforgeryAsync(
            string formGetPath,
            string formPostPath,
            IDictionary<string, string> fields)
        {
            using var getResponse = await GetAsync(formGetPath);
            var html = await getResponse.Content.ReadAsStringAsync();
            var match = AntiforgeryTokenRegex.Match(html);
            Assert.True(match.Success, $"No se encontró __RequestVerificationToken en GET {formGetPath} (Status: {getResponse.StatusCode}).");

            var payload = new Dictionary<string, string>(fields, StringComparer.Ordinal)
            {
                ["__RequestVerificationToken"] = match.Groups[1].Value
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, formPostPath)
            {
                Content = new FormUrlEncodedContent(payload)
            };
            AttachCookies(request);
            var response = await _client.SendAsync(request);
            CaptureCookies(response);
            return response;
        }

        public async Task<HttpResponseMessage> PostFormWithoutAntiforgeryAsync(
            string formPostPath,
            IDictionary<string, string> fields)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, formPostPath)
            {
                Content = new FormUrlEncodedContent(fields)
            };
            AttachCookies(request);
            var response = await _client.SendAsync(request);
            CaptureCookies(response);
            return response;
        }

        private void AttachCookies(HttpRequestMessage request)
        {
            if (_cookies.Count == 0)
            {
                return;
            }

            var cookieHeader = string.Join("; ", _cookies.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            request.Headers.Add("Cookie", cookieHeader);
        }

        private void CaptureCookies(HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
            {
                return;
            }

            foreach (var rawHeader in setCookieHeaders)
            {
                var firstSegment = rawHeader.Split(';', 2)[0];
                var eqIndex = firstSegment.IndexOf('=', StringComparison.Ordinal);
                if (eqIndex <= 0)
                {
                    continue;
                }

                var name = firstSegment[..eqIndex].Trim();
                var value = firstSegment[(eqIndex + 1)..].Trim();

                if (string.IsNullOrEmpty(value) ||
                    rawHeader.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase))
                {
                    _cookies.Remove(name);
                }
                else
                {
                    _cookies[name] = value;
                }
            }
        }

        public void Dispose() => _client.Dispose();
    }
}
