using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Paye.Core.Authorization;
using Paye.Core.Demo;
using Paye.Infrastructure.Identity;

namespace Paye.Web.Controllers.Api;

public sealed record LoginApiRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    bool RememberMe = false);

public sealed record Verify2faApiRequest(
    [Required, EmailAddress] string Email,
    [Required, StringLength(7, MinimumLength = 6)] string Code,
    bool RememberMe = false);

public sealed record ApiUserDto(
    string Id,
    string Name,
    string Email,
    string Avatar,
    string Role,
    IReadOnlyList<string> Permissions);

public sealed record LoginApiResponse(
    bool RequiresTwoFactor,
    string? Email = null,
    ApiUserDto? User = null);

public sealed record AuthConfigResponse(bool DemoSandboxEnabled);

[ApiController]
[Route("api/auth")]
public sealed class ApiAuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    RoleManager<IdentityRole> roleManager,
    IDemoSandboxService demoSandboxService,
    ILogger<ApiAuthController> logger) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("config")]
    [ProducesResponseType<AuthConfigResponse>(StatusCodes.Status200OK)]
    public IActionResult GetConfig()
    {
        return Ok(new AuthConfigResponse(demoSandboxService.IsEnabled));
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-strict")]
    [IgnoreAntiforgeryToken]
    [HttpPost("login")]
    [ProducesResponseType<LoginApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginApiRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Credenciales incorrectas",
                Detail = "El correo o la contraseña son incorrectos."
            });
        }

        if (!user.IsActive)
        {
            logger.LogWarning("Intento de acceso denegado para usuario inactivo {UserId}.", user.Id);
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Cuenta desactivada",
                Detail = "Esta cuenta ha sido desactivada por el administrador."
            });
        }

        var result = await signInManager.PasswordSignInAsync(
            user.UserName!,
            request.Password,
            request.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            logger.LogInformation("Inicio de sesión API exitoso para {UserId}.", user.Id);
            var userDto = await BuildUserDtoAsync(user);
            return Ok(new LoginApiResponse(RequiresTwoFactor: false, User: userDto));
        }

        if (result.RequiresTwoFactor)
        {
            return Ok(new LoginApiResponse(RequiresTwoFactor: true, Email: user.Email));
        }

        if (result.IsLockedOut)
        {
            logger.LogWarning("Cuenta bloqueada temporalmente: {UserId}.", user.Id);
            return StatusCode(StatusCodes.Status423Locked, new ProblemDetails
            {
                Status = StatusCodes.Status423Locked,
                Title = "Cuenta bloqueada",
                Detail = "Cuenta bloqueada temporalmente tras 5 intentos fallidos. Inténtalo de nuevo en 15 minutos."
            });
        }

        return Unauthorized(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Credenciales incorrectas",
            Detail = "El correo o la contraseña son incorrectos."
        });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-strict")]
    [IgnoreAntiforgeryToken]
    [HttpPost("2fa")]
    [ProducesResponseType<ApiUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked)]
    public async Task<IActionResult> Verify2fa([FromBody] Verify2faApiRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null || !user.IsActive)
        {
            user = await userManager.FindByEmailAsync(request.Email.Trim());
        }

        if (user is null || !user.IsActive)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error de verificación",
                Detail = "No se pudo verificar la sesión de dos factores."
            });
        }

        var cleanCode = request.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var result = await signInManager.TwoFactorAuthenticatorSignInAsync(
            cleanCode,
            request.RememberMe,
            rememberClient: false);

        if (result.Succeeded)
        {
            logger.LogInformation("Verificación 2FA correcta para {UserId}.", user.Id);
            var userDto = await BuildUserDtoAsync(user);
            return Ok(userDto);
        }

        if (result.IsLockedOut)
        {
            return StatusCode(StatusCodes.Status423Locked, new ProblemDetails
            {
                Status = StatusCodes.Status423Locked,
                Title = "Cuenta bloqueada",
                Detail = "Cuenta bloqueada temporalmente tras intentos fallidos. Inténtalo de nuevo en 15 minutos."
            });
        }

        return BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Código no válido",
            Detail = "Código 2FA incorrecto o expirado."
        });
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return Ok(new { message = "Sesión cerrada correctamente." });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-strict")]
    [IgnoreAntiforgeryToken]
    [HttpPost("demo-sandbox")]
    [ProducesResponseType<ApiUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DemoSandboxLogin(CancellationToken cancellationToken)
    {
        if (!demoSandboxService.IsEnabled)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "No disponible",
                Detail = "El modo Demo Sandbox no está activado en este entorno."
            });
        }

        var sessionResult = await demoSandboxService.CreateIsolatedDemoSessionAsync(cancellationToken);
        if (!sessionResult.Succeeded || string.IsNullOrWhiteSpace(sessionResult.DemoUserId))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error al crear demo",
                Detail = sessionResult.ErrorMessage ?? "No se pudo iniciar la sesión de demostración aislada."
            });
        }

        var demoUser = await userManager.FindByIdAsync(sessionResult.DemoUserId);
        if (demoUser is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Usuario demo no encontrado",
                Detail = "No se pudo recuperar la cuenta de demostración generada."
            });
        }

        await signInManager.SignInAsync(demoUser, isPersistent: false);

        var userDto = await BuildUserDtoAsync(demoUser);
        return Ok(userDto);
    }

    private async Task<ApiUserDto> BuildUserDtoAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var primaryRole = roles.FirstOrDefault() ?? SystemRoles.Empleado;

        var permissionCodes = new HashSet<string>(StringComparer.Ordinal);

        // Claims directos del usuario
        var userClaims = await userManager.GetClaimsAsync(user);
        foreach (var claim in userClaims.Where(c => c.Type == CorePermissions.ClaimType))
        {
            permissionCodes.Add(claim.Value);
        }

        // Claims de los roles del usuario
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
}
