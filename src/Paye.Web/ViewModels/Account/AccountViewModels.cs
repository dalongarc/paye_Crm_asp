using System.ComponentModel.DataAnnotations;

namespace Paye.Web.ViewModels.Account;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "Introduce un correo electrónico válido.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [DataType(DataType.Password)]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class LoginTwoFactorViewModel
{
    [Required(ErrorMessage = "El código de verificación es obligatorio.")]
    [StringLength(10, MinimumLength = 6, ErrorMessage = "Introduce el código de 6 dígitos de tu aplicación autenticadora.")]
    public string TwoFactorCode { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "Introduce un correo electrónico válido.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordViewModel
{
    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El token de restablecimiento es obligatorio.")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "La contraseña debe tener al menos 12 caracteres.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirma la nueva contraseña.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class ChangePasswordViewModel
{
    [Required(ErrorMessage = "La contraseña actual es obligatoria.")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "La nueva contraseña debe tener al menos 12 caracteres.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirma la nueva contraseña.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class TwoFactorSetupViewModel
{
    public bool IsTwoFactorEnabled { get; set; }

    public string SharedKey { get; set; } = string.Empty;

    public string AuthenticatorUri { get; set; } = string.Empty;

    [Required(ErrorMessage = "Introduce el código de 6 dígitos generado por tu aplicación autenticadora.")]
    [StringLength(10, MinimumLength = 6)]
    public string VerificationCode { get; set; } = string.Empty;
}
