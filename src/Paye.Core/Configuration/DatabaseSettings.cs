using System.ComponentModel.DataAnnotations;

namespace Paye.Core.Configuration;

public sealed class DatabaseSettings : IValidatableObject
{
    public const string SectionName = "ConnectionStrings";

    [Required(ErrorMessage = "La cadena de conexión ConnectionStrings:DefaultConnection es obligatoria (fail-closed).")]
    public string DefaultConnection { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(DefaultConnection))
        {
            yield return new ValidationResult(
                "ConnectionStrings:DefaultConnection no puede estar vacía.",
                [nameof(DefaultConnection)]);
            yield break;
        }

        if (DefaultConnection.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "ConnectionStrings:DefaultConnection contiene el marcador 'CHANGE_ME'. Configura credenciales reales en el entorno.",
                [nameof(DefaultConnection)]);
        }

        if (!DefaultConnection.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
            !DefaultConnection.Contains("Database=", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "ConnectionStrings:DefaultConnection debe ser una cadena de conexión PostgreSQL válida (Host=...;Database=...).",
                [nameof(DefaultConnection)]);
        }
    }
}
