using System.ComponentModel.DataAnnotations;

namespace Paye.Core.Configuration;

public sealed class SmtpSettings : IValidatableObject
{
    public const string SectionName = "Smtp";

    public string? Host { get; set; }

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? FromEmail { get; set; }

    public string FromName { get; set; } = "Payé Digital";

    public bool EnableSsl { get; set; } = true;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        !Host.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(FromEmail);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(FromEmail) &&
            !new EmailAddressAttribute().IsValid(FromEmail))
        {
            yield return new ValidationResult(
                "Smtp:FromEmail no tiene un formato de correo válido.",
                [nameof(FromEmail)]);
        }
    }
}
