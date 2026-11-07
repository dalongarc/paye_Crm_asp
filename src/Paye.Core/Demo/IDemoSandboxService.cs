namespace Paye.Core.Demo;

public static class DemoSandboxConstants
{
    public const string ClaimType = "paye.demo_sandbox";
    public const string ClaimValue = "true";
    public const string DemoUserIdPrefix = "demo-";
    public const string DemoEmailDomain = "@sandbox.paye.local";

    public static bool IsDemoUserId(string? userId) =>
        !string.IsNullOrWhiteSpace(userId) &&
        userId.StartsWith(DemoUserIdPrefix, StringComparison.Ordinal);
}

public sealed record DemoSessionStartResult(
    bool Succeeded,
    string? DemoUserId = null,
    string? DemoEmail = null,
    string? ErrorMessage = null);

/// <summary>
/// Contrato modular para que cada módulo autocontenido (Notas, CRM, POS, etc.)
/// declare sus permisos permitidos en modo Demo, siembre sus datos de ejemplo
/// aislados y limpie sus registros al cerrar la sesión de demostración.
/// </summary>
public interface IDemoModuleDataProvider
{
    IReadOnlyList<string> GetDemoPermissions();

    Task SeedDemoDataAsync(
        string demoUserId,
        CancellationToken cancellationToken = default);

    Task CleanupDemoDataAsync(
        string demoUserId,
        CancellationToken cancellationToken = default);
}

public interface IDemoSandboxService
{
    bool IsEnabled { get; }

    Task<DemoSessionStartResult> CreateIsolatedDemoSessionAsync(
        CancellationToken cancellationToken = default);

    Task CleanupDemoSessionAsync(
        string demoUserId,
        CancellationToken cancellationToken = default);
}
