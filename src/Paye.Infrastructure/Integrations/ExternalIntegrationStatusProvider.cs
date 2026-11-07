using Microsoft.Extensions.Options;
using Paye.Core.Integrations;

namespace Paye.Infrastructure.Integrations;

public sealed class ExternalIntegrationStatusProvider(
    IOptions<ExternalIntegrationsSettings> options) : IExternalIntegrationStatusProvider
{
    public bool IsGeminiEnabled => options.Value.Gemini.IsEnabled;

    public bool IsGoogleEnabled => options.Value.Google.IsEnabled;
}
