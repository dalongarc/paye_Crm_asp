using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Paye.Core.Auditing;
using Paye.Core.Authorization;
using Paye.Core.Configuration;
using Paye.Core.Demo;
using Paye.Core.Email;
using Paye.Core.Files;
using Paye.Core.Gdpr;
using Paye.Core.Integrations;
using Paye.Infrastructure.Auditing;
using Paye.Infrastructure.Data;
using Paye.Infrastructure.Data.Interceptors;
using Paye.Infrastructure.Demo;
using Paye.Infrastructure.Email;
using Paye.Infrastructure.Files;
using Paye.Infrastructure.Gdpr;
using Paye.Infrastructure.Identity;
using Paye.Infrastructure.Integrations;

namespace Paye.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPayeInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration;

        services.AddScoped<AuditableEntitySaveChangesInterceptor>();

        services.AddDbContext<PayeDbContext>((serviceProvider, options) =>
        {
            var dbSettings = serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;
            var auditInterceptor = serviceProvider.GetRequiredService<AuditableEntitySaveChangesInterceptor>();

            options.UseNpgsql(dbSettings.DefaultConnection, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(PayeDbContext).Assembly.FullName);
            });

            options.AddInterceptors(auditInterceptor);
        });

        services.AddSingleton<IPermissionDefinitionProvider, CorePermissionDefinitionProvider>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IdentitySeeder>();
        services.AddScoped<IDemoSandboxService, DemoSandboxService>();
        services.AddScoped<IAuditLogger, DatabaseAuditLogger>();
        services.AddSingleton<IPrivateFileStorage, LocalPrivateFileStorage>();
        services.AddScoped<IGdprUserDataService, GdprUserDataService>();
        services.AddSingleton<IExternalIntegrationStatusProvider, ExternalIntegrationStatusProvider>();

        return services;
    }
}
