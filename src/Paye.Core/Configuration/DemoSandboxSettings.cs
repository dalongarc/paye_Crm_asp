namespace Paye.Core.Configuration;

public sealed class DemoSandboxSettings
{
    public const string SectionName = "DemoSandbox";

    /// <summary>
    /// Desactivado por defecto (Fail-closed).
    /// Solo debe habilitarse en entornos locales o contenedores de demostración aislados.
    /// </summary>
    public bool Enabled { get; set; }
}
