using System.ComponentModel.DataAnnotations;

namespace Paye.Core.Configuration;

public sealed class ClientSettings : IValidatableObject
{
    public const string SectionName = "ClientSettings";

    [Required(ErrorMessage = "El nombre del negocio (ClientSettings:BusinessName) es obligatorio.")]
    [StringLength(120, MinimumLength = 2)]
    public string BusinessName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "El correo de contacto (ClientSettings:ContactEmail) no tiene un formato válido.")]
    [StringLength(150)]
    public string? ContactEmail { get; set; }

    [StringLength(40)]
    public string? ContactPhone { get; set; }

    [StringLength(200)]
    public string LogoPath { get; set; } = "/img/logo-placeholder.svg";

    [Required]
    [StringLength(64)]
    public string TimeZoneId { get; set; } = "Europe/Madrid";

    [Required]
    [StringLength(16)]
    public string DefaultCulture { get; set; } = "es-ES";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(BusinessName) ||
            BusinessName.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "ClientSettings:BusinessName debe configurarse con un valor real y no puede contener 'CHANGE_ME'.",
                [nameof(BusinessName)]);
        }

        if (!string.IsNullOrWhiteSpace(LogoPath) && !LogoPath.StartsWith('/'))
        {
            yield return new ValidationResult(
                "ClientSettings:LogoPath debe ser una ruta relativa local que comience por '/'.",
                [nameof(LogoPath)]);
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(TimeZoneId, out _))
        {
            yield return new ValidationResult(
                $"La zona horaria '{TimeZoneId}' no es válida en el sistema.",
                [nameof(TimeZoneId)]);
        }
    }
}
