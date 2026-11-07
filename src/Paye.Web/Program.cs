using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Paye.Core.Common;
using Paye.Core.Configuration;
using Paye.Core.Integrations;
using Paye.Infrastructure;
using Paye.Infrastructure.Data;
using Paye.Infrastructure.Identity;
using Paye.Infrastructure.Modules.Clientes;
using Paye.Web.Authorization;
using Paye.Web.Middleware;
using Paye.Web.Modules.Facturacion;
using Paye.Web.Modules.Notas;
using Paye.Web.Services;

if (args.Contains("--healthcheck", StringComparer.OrdinalIgnoreCase))
{
    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
    try
    {
        using var response = await httpClient.GetAsync("http://127.0.0.1:8080/health");
        return response.IsSuccessStatusCode ? 0 : 1;
    }
    catch
    {
        return 1;
    }
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.WebHost.ConfigureKestrel(serverOptions => serverOptions.AddServerHeader = false);

// 1. Fail-closed configuration validation on startup (ValidateOnStart)
builder.Services
    .AddOptions<DatabaseSettings>()
    .Bind(builder.Configuration.GetSection(DatabaseSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<ClientSettings>()
    .Bind(builder.Configuration.GetSection(ClientSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<SecuritySettings>()
    .Bind(builder.Configuration.GetSection(SecuritySettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<SmtpSettings>()
    .Bind(builder.Configuration.GetSection(SmtpSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<ExternalIntegrationsSettings>()
    .Bind(builder.Configuration.GetSection(ExternalIntegrationsSettings.SectionName));

builder.Services
    .AddOptions<DemoSandboxSettings>()
    .Bind(builder.Configuration.GetSection(DemoSandboxSettings.SectionName));

var securitySettings = builder.Configuration
    .GetSection(SecuritySettings.SectionName)
    .Get<SecuritySettings>() ?? new SecuritySettings();

// 2. Data Protection persisted to dedicated volume directory
if (!string.IsNullOrWhiteSpace(securitySettings.DataProtectionKeysPath) &&
    !securitySettings.DataProtectionKeysPath.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
{
    var keysDir = new DirectoryInfo(securitySettings.DataProtectionKeysPath);
    if (!keysDir.Exists)
    {
        keysDir.Create();
    }

    builder.Services
        .AddDataProtection()
        .SetApplicationName("Paye.Base")
        .PersistKeysToFileSystem(keysDir);
}
else
{
    builder.Services
        .AddDataProtection()
        .SetApplicationName("Paye.Base");
}

// 3. ForwardedHeaders for reverse proxy (Coolify / Traefik) with explicit trusted proxies
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();

    foreach (var proxy in securitySettings.TrustedProxies)
    {
        if (IPAddress.TryParse(proxy, out var ipAddress))
        {
            options.KnownProxies.Add(ipAddress);
        }
    }
});

// 4. Infrastructure & Persistence
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();
builder.Services.AddPayeInfrastructure(builder.Configuration);

// 5. ASP.NET Core Identity with PostgreSQL
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredUniqueChars = 4;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<PayeDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(2);
});

// Validate SecurityStamp on every request so password changes, role changes, or deactivation invalidate open sessions immediately
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = builder.Environment.IsDevelopment()
        ? "Paye.Auth.Dev"
        : "__Host-Paye.Auth";
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/access-denied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);

    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnValidatePrincipal = async context =>
    {
        await SecurityStampValidator.ValidatePrincipalAsync(context);
        if (context.Principal is null)
        {
            return;
        }

        var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var userId = userManager.GetUserId(context.Principal)
            ?? context.Principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null || !user.IsActive)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    };
});

// 6. Granular Permission Authorization & Deny-by-Default
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// 7. Antiforgery (enforced globally on write endpoints)
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = builder.Environment.IsDevelopment()
        ? "Paye.Antiforgery.Dev"
        : "__Host-Paye.Antiforgery";
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.HeaderName = "RequestVerificationToken";
});

// 8. Explicit CORS (same-origin by default; never reflects wildcard or untrusted Origin)
builder.Services.AddCors(options =>
{
    options.AddPolicy("ExplicitOriginAllowlist", policy =>
    {
        var validOrigins = securitySettings.AllowedOrigins
            .Where(o => !string.IsNullOrWhiteSpace(o) && o != "*")
            .ToArray();

        if (validOrigins.Length > 0)
        {
            policy.WithOrigins(validOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

// 9. Built-in Rate Limiting (login & sensitive endpoints)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth-strict", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

// 10. Centralized ProblemDetails & Exception Handling without internal leaks
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions.Remove("exception");
        ctx.ProblemDetails.Extensions.Remove("stackTrace");
        ctx.ProblemDetails.Detail = "No se pudo procesar la solicitud.";
    };
});

// 11. Health Checks
builder.Services.AddHealthChecks();

// 12. Localization (es-ES default)
builder.Services.AddLocalization();

// 13. Self-contained business modules (delete module folders + remove line below to disable)
builder.Services.AddNotasModule();
builder.Services.AddFacturacionModule();
builder.Services.AddClientesModule();

// 14. Native OpenAPI (.NET 10)
builder.Services.AddOpenApi();

// 14. MVC with global Antiforgery validation and modular Razor view locations
builder.Services
    .AddControllersWithViews(options =>
    {
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    })
    .AddRazorOptions(razor =>
    {
        razor.ViewLocationFormats.Add("/Modules/{1}/Views/{0}.cshtml");
    });

var app = builder.Build();

// Controlled CLI migration / seeding commands (never auto-migrate on Production startup)
if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();
    await db.Database.MigrateAsync();

    var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
    await seeder.SeedRolesAndPermissionsAsync();
    await seeder.SeedInitialOwnerFromEnvironmentAsync();
    return 0;
}

if (args.Contains("--seed", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
    await seeder.SeedRolesAndPermissionsAsync();
    await seeder.SeedInitialOwnerFromEnvironmentAsync();
    return 0;
}

// If SEED_OWNER_EMAIL is explicitly provided on first startup, run idempotent role & owner seeding
if (!string.IsNullOrWhiteSpace(app.Configuration["SEED_OWNER_EMAIL"]))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
    await seeder.SeedRolesAndPermissionsAsync();
    await seeder.SeedInitialOwnerFromEnvironmentAsync();
}

app.UseForwardedHeaders();
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseExceptionHandler("/error");
app.UseStatusCodePagesWithReExecute("/error", "?statusCode={0}");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

var defaultCulture = new CultureInfo("es-ES");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(defaultCulture),
    SupportedCultures = [defaultCulture],
    SupportedUICultures = [defaultCulture]
});

app.UseStaticFiles();
app.UseRouting();

app.UseCors("ExplicitOriginAllowlist");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = static async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var status = report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy
            ? "ok"
            : "unhealthy";
        await context.Response.WriteAsync($"{{\"status\":\"{status}\"}}");
    }
}).AllowAnonymous();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Catch-all para rutas /api no mapeadas: devuelve 404 y evita servir index.html
app.Map("/api/{*path}", () => Results.NotFound());

// SPA Fallback para el frontend de /web servido desde wwwroot (excluye /api y /health)
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();
return 0;

public partial class Program;
