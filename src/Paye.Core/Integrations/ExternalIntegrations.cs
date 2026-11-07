namespace Paye.Core.Integrations;

public sealed class ExternalIntegrationsSettings
{
    public const string SectionName = "Integrations";

    public GeminiIntegrationOptions Gemini { get; init; } = new();
    public GoogleWorkspaceIntegrationOptions Google { get; init; } = new();
}

public sealed class GeminiIntegrationOptions
{
    public string? ApiKey { get; init; }
    public string Model { get; init; } = "gemini-2.5-flash";

    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !ApiKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase);
}

public sealed class GoogleWorkspaceIntegrationOptions
{
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }

    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        !ClientId.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase) &&
        !ClientSecret.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase);
}

public interface IExternalIntegrationStatusProvider
{
    bool IsGeminiEnabled { get; }
    bool IsGoogleEnabled { get; }
}
