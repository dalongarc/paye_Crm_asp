using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Paye.Core.Configuration;
using Paye.Core.Demo;
using Paye.Core.Email;
using Paye.Infrastructure.Identity;
using Paye.Web.ViewModels.Account;

namespace Paye.Web.Controllers;

public sealed class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IDemoSandboxService demoSandboxService,
    IEmailSender emailSender,
    IOptions<ClientSettings> clientOptions,
    UrlEncoder urlEncoder,
    ILogger<AccountController> logger) : Controller
{
    [AllowAnonymous]
    [HttpGet("/account/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        var isBrowser = Request.Headers.UserAgent.ToString().Contains("Mozilla", StringComparison.OrdinalIgnoreCase)
                     || Request.Headers.Accept.ToString().StartsWith("text/html", StringComparison.OrdinalIgnoreCase);
        var spaPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html");

        if (isBrowser && System.IO.File.Exists(spaPath) && !Request.Query.ContainsKey("razor"))
        {
            var redirectUrl = $"/sign-in{(string.IsNullOrEmpty(returnUrl) ? "" : $"?redirect={Uri.EscapeDataString(returnUrl)}")}";
            return Redirect(redirectUrl);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-strict")]
    [HttpPost("/account/demo-login")]
    public async Task<IActionResult> DemoLogin(CancellationToken cancellationToken)
    {
        if (!demoSandboxService.IsEnabled)
        {
            return NotFound();
        }

        var sessionResult = await demoSandboxService.CreateIsolatedDemoSessionAsync(cancellationToken);
        if (!sessionResult.Succeeded || string.IsNullOrWhiteSpace(sessionResult.DemoUserId))
        {
            TempData["ErrorMessage"] = sessionResult.ErrorMessage ?? "No se pudo iniciar la sesión de demostración.";
            return RedirectToAction(nameof(Login));
        }

        var demoUser = await userManager.FindByIdAsync(sessionResult.DemoUserId);
        if (demoUser is null)
        {
            TempData["ErrorMessage"] = "No se pudo encontrar la sesión de demostración creada.";
            return RedirectToAction(nameof(Login));
        }

        await signInManager.SignInAsync(demoUser, isPersistent: false);

        TempData["SuccessMessage"] = "Modo Demo Aislado iniciado con datos de muestra. Puedes crear y editar tus propias notas; al cerrar sesión todo se borrará automáticamente.";
        return RedirectToAction("Index", "Notas");
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-strict")]
    [HttpPost("/account/login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Credenciales no válidas.");
            return View(model);
        }

        if (!user.IsActive)
        {
            logger.LogWarning("Intento de acceso rechazado para usuario desactivado {UserId}.", user.Id);
            ModelState.AddModelError(string.Empty, "Esta cuenta de usuario se encuentra desactivada.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            user.UserName!,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            logger.LogInformation("Inicio de sesión correcto para {UserId}.", user.Id);
            return RedirectToLocal(model.ReturnUrl);
        }

        if (result.RequiresTwoFactor)
        {
            return RedirectToAction(nameof(LoginTwoFactor), new { model.ReturnUrl, model.RememberMe });
        }

        if (result.IsLockedOut)
        {
            logger.LogWarning("Cuenta bloqueada temporalmente por intentos fallidos: {UserId}.", user.Id);
            ModelState.AddModelError(string.Empty, "La cuenta ha sido bloqueada temporalmente por demasiados intentos fallidos. Inténtelo más tarde.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Credenciales no válidas.");
        return View(model);
    }

    [AllowAnonymous]
    [HttpGet("/account/login-2fa")]
    public async Task<IActionResult> LoginTwoFactor(bool rememberMe = false, string? returnUrl = null)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null || !user.IsActive)
        {
            return RedirectToAction(nameof(Login));
        }

        return View(new LoginTwoFactorViewModel
        {
            RememberMe = rememberMe,
            ReturnUrl = returnUrl
        });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-strict")]
    [HttpPost("/account/login-2fa")]
    public async Task<IActionResult> LoginTwoFactor(LoginTwoFactorViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null || !user.IsActive)
        {
            return RedirectToAction(nameof(Login));
        }

        var authenticatorCode = model.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);
        var result = await signInManager.TwoFactorAuthenticatorSignInAsync(
            authenticatorCode,
            model.RememberMe,
            rememberClient: false);

        if (result.Succeeded)
        {
            logger.LogInformation("Inicio de sesión con 2FA completado para {UserId}.", user.Id);
            return RedirectToLocal(model.ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "La cuenta ha sido bloqueada temporalmente por demasiados intentos fallidos.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "El código de verificación no es válido.");
        return View(model);
    }

    [HttpPost("/account/logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var currentUserId = userManager.GetUserId(User);
        await signInManager.SignOutAsync();

        if (!string.IsNullOrWhiteSpace(currentUserId) && DemoSandboxConstants.IsDemoUserId(currentUserId))
        {
            await demoSandboxService.CleanupDemoSessionAsync(currentUserId, cancellationToken);
            TempData["SuccessMessage"] = "Sesión de demostración cerrada. Todos los datos temporales de tu sesión han sido eliminados.";
        }

        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet("/account/forgot-password")]
    public IActionResult ForgotPassword()
    {
        ViewData["SmtpConfigured"] = emailSender.IsConfigured;
        return View(new ForgotPasswordViewModel());
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-strict")]
    [HttpPost("/account/forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        ViewData["SmtpConfigured"] = emailSender.IsConfigured;

        if (!emailSender.IsConfigured)
        {
            ModelState.AddModelError(
                string.Empty,
                "El envío de correo (SMTP) no está configurado en este servidor. Contacte con el propietario para restablecer su contraseña.");
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is not null && user.IsActive)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action(
                nameof(ResetPassword),
                "Account",
                new { email = user.Email, token },
                protocol: Request.Scheme);

            var sendResult = await emailSender.SendEmailAsync(
                user.Email!,
                $"Restablecimiento de contraseña — {clientOptions.Value.BusinessName}",
                $"Para restablecer su contraseña, acceda al siguiente enlace:\n{callbackUrl}",
                cancellationToken);

            if (sendResult.NotConfigured || !sendResult.Succeeded)
            {
                ModelState.AddModelError(
                    string.Empty,
                    sendResult.ErrorMessage ?? "No se pudo enviar el correo de recuperación.");
                return View(model);
            }
        }

        TempData["SuccessMessage"] = "Si el correo corresponde a una cuenta activa, recibirá las instrucciones de recuperación.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet("/account/reset-password")]
    public IActionResult ResetPassword(string? email = null, string? token = null)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            return RedirectToAction(nameof(Login));
        }

        return View(new ResetPasswordViewModel
        {
            Email = email,
            Token = token
        });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-strict")]
    [HttpPost("/account/reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null || !user.IsActive)
        {
            TempData["SuccessMessage"] = "El proceso de restablecimiento de contraseña ha finalizado.";
            return RedirectToAction(nameof(Login));
        }

        var result = await userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        await userManager.UpdateSecurityStampAsync(user);
        TempData["SuccessMessage"] = "Contraseña actualizada correctamente. Ya puede iniciar sesión.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("/account/change-password")]
    public IActionResult ChangePassword()
    {
        if (DemoSandboxConstants.IsDemoUserId(userManager.GetUserId(User)))
        {
            TempData["ErrorMessage"] = "El cambio de contraseña no está disponible en la sesión de demostración aislada.";
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        return View(new ChangePasswordViewModel());
    }

    [HttpPost("/account/change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (DemoSandboxConstants.IsDemoUserId(userManager.GetUserId(User)))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null || !user.IsActive)
        {
            await signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        var result = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        await userManager.UpdateSecurityStampAsync(user);
        await signInManager.RefreshSignInAsync(user);

        TempData["SuccessMessage"] = "Su contraseña ha sido actualizada y el resto de sesiones se han invalidado.";
        return RedirectToAction(nameof(ChangePassword));
    }

    [HttpGet("/account/two-factor")]
    public async Task<IActionResult> TwoFactor()
    {
        if (DemoSandboxConstants.IsDemoUserId(userManager.GetUserId(User)))
        {
            TempData["ErrorMessage"] = "La configuración 2FA no está disponible en la sesión de demostración aislada.";
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null || !user.IsActive)
        {
            return RedirectToAction(nameof(Login));
        }

        var model = await BuildTwoFactorViewModelAsync(user);
        return View(model);
    }

    [HttpPost("/account/two-factor/enable")]
    public async Task<IActionResult> EnableTwoFactor(TwoFactorSetupViewModel model)
    {
        if (DemoSandboxConstants.IsDemoUserId(userManager.GetUserId(User)))
        {
            return Forbid();
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null || !user.IsActive)
        {
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            var refreshed = await BuildTwoFactorViewModelAsync(user);
            refreshed.VerificationCode = model.VerificationCode;
            return View("TwoFactor", refreshed);
        }

        var verificationCode = model.VerificationCode.Replace(" ", string.Empty).Replace("-", string.Empty);
        var isTokenValid = await userManager.VerifyTwoFactorTokenAsync(
            user,
            userManager.Options.Tokens.AuthenticatorTokenProvider,
            verificationCode);

        if (!isTokenValid)
        {
            ModelState.AddModelError(nameof(model.VerificationCode), "El código de verificación TOTP no es válido.");
            var refreshed = await BuildTwoFactorViewModelAsync(user);
            return View("TwoFactor", refreshed);
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        await userManager.UpdateSecurityStampAsync(user);
        await signInManager.RefreshSignInAsync(user);

        logger.LogInformation("Usuario {UserId} ha activado la autenticación en dos pasos (TOTP).", user.Id);
        TempData["SuccessMessage"] = "La autenticación en dos pasos (2FA TOTP) se ha activado correctamente.";
        return RedirectToAction(nameof(TwoFactor));
    }

    [HttpPost("/account/two-factor/disable")]
    public async Task<IActionResult> DisableTwoFactor()
    {
        if (DemoSandboxConstants.IsDemoUserId(userManager.GetUserId(User)))
        {
            return Forbid();
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null || !user.IsActive)
        {
            return RedirectToAction(nameof(Login));
        }

        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        await userManager.UpdateSecurityStampAsync(user);
        await signInManager.RefreshSignInAsync(user);

        logger.LogInformation("Usuario {UserId} ha desactivado la autenticación en dos pasos (TOTP).", user.Id);
        TempData["SuccessMessage"] = "La autenticación en dos pasos se ha desactivado.";
        return RedirectToAction(nameof(TwoFactor));
    }

    [AllowAnonymous]
    [HttpGet("/account/access-denied")]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private async Task<TwoFactorSetupViewModel> BuildTwoFactorViewModelAsync(ApplicationUser user)
    {
        var unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(unformattedKey))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);
            await signInManager.RefreshSignInAsync(user);
        }

        var is2faEnabled = await userManager.GetTwoFactorEnabledAsync(user);
        var issuer = urlEncoder.Encode(clientOptions.Value.BusinessName);
        var account = urlEncoder.Encode(user.Email ?? user.UserName ?? "usuario");

        return new TwoFactorSetupViewModel
        {
            IsTwoFactorEnabled = is2faEnabled,
            SharedKey = FormatKey(unformattedKey ?? string.Empty),
            AuthenticatorUri = $"otpauth://totp/{issuer}:{account}?secret={unformattedKey}&issuer={issuer}&digits=6"
        };
    }

    private static string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        var currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }
        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }
        return result.ToString().ToLowerInvariant();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(HomeController.Index), "Home");
    }
}
