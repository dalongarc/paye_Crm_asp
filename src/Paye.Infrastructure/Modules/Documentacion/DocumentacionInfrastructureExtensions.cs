using Microsoft.Extensions.DependencyInjection;
using Paye.Core.Authorization;
using Paye.Core.Modules.Documentacion;
using Paye.Core.Navigation;

namespace Paye.Infrastructure.Modules.Documentacion;

public static class DocumentacionInfrastructureExtensions
{
    public static IServiceCollection AddDocumentacionModule(this IServiceCollection services)
    {
        services.AddScoped<IDocumentacionService, DocumentacionService>();
        services.AddScoped<IDocumentoPdfExportService, DocumentoPdfExportService>();
        
        // AI Services
        services.AddScoped<IDocumentoAiGenerationService, DocumentoAiGenerationService>();
        services.AddSingleton<DocumentoAiUserRateLimiter>();

        services.AddSingleton<IPermissionDefinitionProvider, DocumentacionPermissionDefinitionProvider>();
        services.AddSingleton<IModuleNavigationProvider, DocumentacionNavigationProvider>();

        return services;
    }
}
