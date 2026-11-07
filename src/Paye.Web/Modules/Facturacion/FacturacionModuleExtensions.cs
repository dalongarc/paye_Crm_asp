using Microsoft.Extensions.DependencyInjection;
using Paye.Core.Navigation;
using Paye.Infrastructure.Modules.Facturacion;

namespace Paye.Web.Modules.Facturacion;

public static class FacturacionModuleExtensions
{
    /// <summary>
    /// Registra el módulo autocontenido de Facturación (permisos, servicios de negocio, PDF nativo y navegación).
    /// Para eliminar este módulo en un proyecto de cliente, basta con borrar las carpetas Modules/Facturacion
    /// y quitar la llamada a AddFacturacionModule() en Program.cs.
    /// </summary>
    public static IServiceCollection AddFacturacionModule(this IServiceCollection services)
    {
        services.AddFacturacionInfrastructure();
        services.AddSingleton<IModuleNavigationProvider, FacturacionNavigationProvider>();
        return services;
    }
}
