using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Paye.Tests.Infrastructure;

public sealed class PayeWebFactory : WebApplicationFactory<Program>
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "paye-tests-" + Guid.NewGuid().ToString("N"));
    private readonly IDictionary<string, string?> _overrides;
    private readonly string _environment;

    public PayeWebFactory(IDictionary<string, string?>? overrides = null, string environment = "Production")
    {
        _overrides = overrides ?? new Dictionary<string, string?>();
        _environment = environment;
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var defaultTestDb = "Host=127.0.0.1;Port=5432;Database=paye_isolated_test_db;Username=paye_test;Password=TestOnly_NotProd_123!";
        TestDatabaseGuard.EnsureIsolatedTestDatabase(defaultTestDb);

        var keysPath = Path.Combine(_tempRoot, "dp-keys");
        var uploadsPath = Path.Combine(_tempRoot, "private-uploads");
        Directory.CreateDirectory(keysPath);
        Directory.CreateDirectory(uploadsPath);

        builder.UseEnvironment(_environment);

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = defaultTestDb,
                ["ClientSettings:BusinessName"] = "Comercio Pruebas Payé",
                ["ClientSettings:ContactEmail"] = "pruebas@payedigital.test",
                ["ClientSettings:LogoPath"] = "/img/logo-placeholder.svg",
                ["ClientSettings:TimeZoneId"] = "Europe/Madrid",
                ["ClientSettings:DefaultCulture"] = "es-ES",
                ["Security:DataProtectionKeysPath"] = keysPath,
                ["Security:PrivateUploadsPath"] = uploadsPath
            };

            foreach (var kvp in _overrides)
            {
                settings[kvp.Key] = kvp.Value;
            }

            configBuilder.AddInMemoryCollection(settings);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_tempRoot))
        {
            try
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
            catch
            {
                // Ignore cleanup contention in temp directory
            }
        }
    }
}
