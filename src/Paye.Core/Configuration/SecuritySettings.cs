using System.ComponentModel.DataAnnotations;
using System.Net;

namespace Paye.Core.Configuration;

public sealed class SecuritySettings : IValidatableObject
{
    public const string SectionName = "Security";

    [Required(ErrorMessage = "La ruta de claves DataProtection (Security:DataProtectionKeysPath) es obligatoria.")]
    public string DataProtectionKeysPath { get; set; } = string.Empty;

    [Required(ErrorMessage = "La ruta de archivos privados (Security:PrivateUploadsPath) es obligatoria.")]
    public string PrivateUploadsPath { get; set; } = string.Empty;

    public string[] AllowedOrigins { get; set; } = [];

    public string[] TrustedProxies { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(DataProtectionKeysPath) ||
            DataProtectionKeysPath.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "Security:DataProtectionKeysPath debe estar configurado con un directorio válido.",
                [nameof(DataProtectionKeysPath)]);
        }

        if (string.IsNullOrWhiteSpace(PrivateUploadsPath) ||
            PrivateUploadsPath.Contains("wwwroot", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "Security:PrivateUploadsPath debe estar configurado y nunca puede apuntar dentro de 'wwwroot'.",
                [nameof(PrivateUploadsPath)]);
        }

        foreach (var origin in AllowedOrigins)
        {
            if (string.IsNullOrWhiteSpace(origin) || origin.Trim() == "*")
            {
                yield return new ValidationResult(
                    "Security:AllowedOrigins no permite comodines ('*') ni valores vacíos. Usa orígenes explícitos (https://dominio.es).",
                    [nameof(AllowedOrigins)]);
                continue;
            }

            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                yield return new ValidationResult(
                    $"El origen CORS '{origin}' no es una URL absoluta HTTP/HTTPS válida.",
                    [nameof(AllowedOrigins)]);
            }
        }

        foreach (var proxy in TrustedProxies)
        {
            if (!IPAddress.TryParse(proxy, out _))
            {
                yield return new ValidationResult(
                    $"La dirección de proxy de confianza '{proxy}' en Security:TrustedProxies no es una IP válida.",
                    [nameof(TrustedProxies)]);
            }
        }
    }
}
