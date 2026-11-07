using Microsoft.Extensions.DependencyInjection;
using Paye.Core.Authorization;
using Paye.Core.Demo;
using Paye.Core.Invoicing;
using Paye.Core.Modules.Facturacion;

namespace Paye.Infrastructure.Modules.Facturacion;

public static class FacturacionInfrastructureExtensions
{
    public static IServiceCollection AddFacturacionInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IPermissionDefinitionProvider, FacturacionPermissionDefinitionProvider>();
        services.AddSingleton<IPdfInvoiceGenerator, PdfInvoiceGenerator>();
        services.AddScoped<IFiscalInvoiceRegistry, NoOpFiscalInvoiceRegistry>();
        services.AddScoped<FacturacionService>();
        services.AddScoped<IFacturacionService>(sp => sp.GetRequiredService<FacturacionService>());
        services.AddScoped<IInvoiceIssuer>(sp => sp.GetRequiredService<FacturacionService>());
        services.AddScoped<ITaxCatalogReader>(sp => sp.GetRequiredService<FacturacionService>());
        services.AddScoped<IDemoModuleDataProvider, FacturacionDemoDataProvider>();
        return services;
    }
}
