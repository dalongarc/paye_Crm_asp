using Microsoft.Extensions.DependencyInjection;
using Paye.Core.Authorization;
using Paye.Core.Demo;
using Paye.Core.Modules.Notas;

namespace Paye.Infrastructure.Modules.Notas;

public static class NotasInfrastructureExtensions
{
    public static IServiceCollection AddNotasInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IPermissionDefinitionProvider, NotasPermissionDefinitionProvider>();
        services.AddScoped<INotasService, NotasService>();
        services.AddScoped<IDemoModuleDataProvider, NotasDemoDataProvider>();
        return services;
    }
}
