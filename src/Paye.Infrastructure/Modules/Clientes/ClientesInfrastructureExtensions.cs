using Microsoft.Extensions.DependencyInjection;
using Paye.Core.Authorization;
using Paye.Core.Modules.Clientes;
using Paye.Core.Navigation;

namespace Paye.Infrastructure.Modules.Clientes;

public static class ClientesInfrastructureExtensions
{
    public static IServiceCollection AddClientesModule(this IServiceCollection services)
    {
        services.AddScoped<IClientesService, ClientesService>();
        services.AddSingleton<IPermissionDefinitionProvider, ClientesPermissionDefinitionProvider>();
        services.AddSingleton<IModuleNavigationProvider, ClientesNavigationProvider>();

        return services;
    }
}
