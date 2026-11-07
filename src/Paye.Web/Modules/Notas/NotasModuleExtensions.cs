using Microsoft.Extensions.DependencyInjection;
using Paye.Core.Navigation;
using Paye.Infrastructure.Modules.Notas;

namespace Paye.Web.Modules.Notas;

public static class NotasModuleExtensions
{
    /// <summary>
    /// Registra el módulo autocontenido de Notas (permisos, servicios de negocio y navegación).
    /// Para eliminar este módulo en un proyecto de cliente, basta con borrar las carpetas Modules/Notas
    /// (y Views/Notas) y quitar la llamada a AddNotasModule() en Program.cs.
    /// </summary>
    public static IServiceCollection AddNotasModule(this IServiceCollection services)
    {
        services.AddNotasInfrastructure();
        services.AddSingleton<IModuleNavigationProvider, NotasNavigationProvider>();
        return services;
    }
}
