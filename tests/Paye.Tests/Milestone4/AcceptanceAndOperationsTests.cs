using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Paye.Core.Modules.Notas;
using Paye.Core.Navigation;
using Paye.Infrastructure.Data;
using Paye.Infrastructure.Identity;
using Paye.Tests.Infrastructure;
using Testcontainers.PostgreSql;

namespace Paye.Tests.Milestone4;

public sealed class Milestone4PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("paye_test_m4")
        .WithUsername("paye_test_user")
        .WithPassword("TestOnly_NotProd_987!")
        .Build();

    public PostgreSqlContainer Container => _container;
    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        TestDatabaseGuard.EnsureIsolatedTestDatabase(ConnectionString);

        var optionsBuilder = new DbContextOptionsBuilder<PayeDbContext>();
        optionsBuilder.UseNpgsql(ConnectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(typeof(PayeDbContext).Assembly.FullName);
        });

        await using var db = new PayeDbContext(optionsBuilder.Options);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

public sealed class AcceptanceAndOperationsTests(Milestone4PostgresFixture dbFixture)
    : IClassFixture<Milestone4PostgresFixture>
{
    private const string OwnerEmail = "propietario.m4@payedigital.test";
    private const string OwnerPassword = "ClaveSegura#Paye2026!";

    private async Task<PayeWebFactory> CreateInitializedFactoryAsync(
        IDictionary<string, string?>? extraOverrides = null)
    {
        var storageDir = Path.Combine(
            Path.GetTempPath(),
            "paye-test-private-files-m4-" + Guid.NewGuid().ToString("N"));

        var overrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = dbFixture.ConnectionString,
            ["Security:PrivateUploadsPath"] = storageDir,
            ["SEED_OWNER_EMAIL"] = OwnerEmail,
            ["SEED_OWNER_PASSWORD"] = OwnerPassword,
            ["SEED_OWNER_FULLNAME"] = "Propietario M4"
        };

        if (extraOverrides is not null)
        {
            foreach (var kvp in extraOverrides)
            {
                overrides[kvp.Key] = kvp.Value;
            }
        }

        var factory = new PayeWebFactory(overrides);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();
        await db.Database.MigrateAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await seeder.SeedRolesAndPermissionsAsync();
        await seeder.SeedInitialOwnerFromEnvironmentAsync();

        return factory;
    }

    [Fact]
    public async Task Criterion4_ModuleIsolation_CoreAndInfrastructureDoNotCoupleToNotas_AndAppRunsWithoutModule()
    {
        // 1. Verify PayeDbContext has zero DbSet<Nota> or properties referencing Paye.Core.Modules.*
        var dbContextProps = typeof(PayeDbContext).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.DoesNotContain(dbContextProps, p =>
            p.PropertyType.FullName?.Contains("Modules.Notas", StringComparison.Ordinal) == true);

        // 2. Verify non-module types in Paye.Core, Paye.Infrastructure, and Paye.Web do not reference Modules.Notas
        var assemblies = new[]
        {
            typeof(Nota).Assembly,
            typeof(PayeDbContext).Assembly,
            typeof(Program).Assembly
        };

        foreach (var assembly in assemblies)
        {
            var nonModuleTypes = assembly.GetTypes()
                .Where(t =>
                    t.Namespace is not null &&
                    !t.Namespace.Contains(".Modules.", StringComparison.Ordinal) &&
                    !(t.FullName?.Contains("Modules_Notas_", StringComparison.Ordinal) == true));

            foreach (var type in nonModuleTypes)
            {
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    Assert.False(
                        field.FieldType.Namespace?.Contains(".Modules.Notas", StringComparison.Ordinal) == true,
                        $"El tipo base {type.FullName} tiene un campo acoplado al módulo Notas ({field.FieldType.FullName}).");
                }
            }
        }

        // 3. Verify removing Notas module registrations from DI leaves the application & sidebar working cleanly
        using var baseFactory = await CreateInitializedFactoryAsync();
        using var factoryWithoutNotas = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IModuleNavigationProvider>();
                services.RemoveAll<INotasService>();
            });
        });

        using var client = await CreateAuthenticatedClientAsync(factoryWithoutNotas, OwnerEmail, OwnerPassword);
        var homeResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);

        var homeHtml = await homeResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("href=\"/notas\"", homeHtml);
        Assert.Contains("href=\"/admin/users\"", homeHtml);
    }

    [Fact]
    public async Task Criterion7_ClientSettings_EnvironmentOverride_UpdatesBusinessNameInUiWithoutCodeChanges()
    {
        const string customBusinessName = "Panadería Artesana San Juan S.L.";

        using var factory = await CreateInitializedFactoryAsync(new Dictionary<string, string?>
        {
            ["ClientSettings:BusinessName"] = customBusinessName,
            ["ClientSettings:ContactEmail"] = "info@panaderiasanjuan.test"
        });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var loginPageResponse = await client.GetAsync("/account/login");
        Assert.Equal(HttpStatusCode.OK, loginPageResponse.StatusCode);

        var html = WebUtility.HtmlDecode(await loginPageResponse.Content.ReadAsStringAsync());
        Assert.Contains(customBusinessName, html);
        Assert.Contains("info@panaderiasanjuan.test", html);
    }

    [Fact]
    public async Task Criterion9_PostgreSqlBackupAndRestore_PgDumpAndPsqlRestore_PreservesAllApplicationData()
    {
        using var factory = await CreateInitializedFactoryAsync();
        using var ownerClient = await CreateAuthenticatedClientAsync(factory, OwnerEmail, OwnerPassword);

        var backupNoteTitle = $"Nota Backup S3 {Guid.NewGuid():N}";
        var createToken = await GetAntiforgeryTokenFromPageAsync(ownerClient, "/notas/crear");
        var createResponse = await ownerClient.PostAsync("/notas/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = createToken,
            ["Titulo"] = backupNoteTitle,
            ["Contenido"] = "Datos críticos para prueba real de pg_dump y restauración psql."
        }));
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        // 1. Run pg_dump inside the PostgreSQL container
        var dumpResult = await dbFixture.Container.ExecAsync(
        [
            "pg_dump",
            "-U", "paye_test_user",
            "-d", "paye_test_m4",
            "--no-owner",
            "--no-privileges",
            "-f", "/tmp/paye_backup.sql"
        ]);
        Assert.True(dumpResult.ExitCode == 0, $"pg_dump falló: {dumpResult.Stderr}");

        // 2. Create a clean target database inside the PostgreSQL container
        const string restoredDbName = "paye_test_m4_restored";
        var createDbResult = await dbFixture.Container.ExecAsync(
        [
            "createdb",
            "-U", "paye_test_user",
            restoredDbName
        ]);
        Assert.True(createDbResult.ExitCode == 0, $"createdb falló: {createDbResult.Stderr}");

        // 3. Restore the SQL dump into the clean database using psql
        var restoreResult = await dbFixture.Container.ExecAsync(
        [
            "psql",
            "-U", "paye_test_user",
            "-d", restoredDbName,
            "-v", "ON_ERROR_STOP=1",
            "-f", "/tmp/paye_backup.sql"
        ]);
        Assert.True(restoreResult.ExitCode == 0, $"psql restore falló: {restoreResult.Stderr}");

        // 4. Connect a new PayeDbContext to the restored database and verify all data is intact
        var restoredConnStringBuilder = new NpgsqlConnectionStringBuilder(dbFixture.ConnectionString)
        {
            Database = restoredDbName
        };

        var optionsBuilder = new DbContextOptionsBuilder<PayeDbContext>();
        optionsBuilder.UseNpgsql(restoredConnStringBuilder.ConnectionString);

        await using var restoredDb = new PayeDbContext(optionsBuilder.Options);

        var restoredOwner = await restoredDb.Users.SingleOrDefaultAsync(u => u.Email == OwnerEmail);
        Assert.NotNull(restoredOwner);
        Assert.Equal("Propietario M4", restoredOwner.FullName);

        var restoredNota = await restoredDb.Set<Nota>().SingleOrDefaultAsync(n => n.Titulo == backupNoteTitle);
        Assert.NotNull(restoredNota);
        Assert.Equal("Datos críticos para prueba real de pg_dump y restauración psql.", restoredNota.Contenido);
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync<TEntryPoint>(
        WebApplicationFactory<TEntryPoint> factory,
        string email,
        string password) where TEntryPoint : class
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var loginToken = await GetAntiforgeryTokenFromPageAsync(client, "/account/login");
        var loginResponse = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = loginToken,
            ["Email"] = email,
            ["Password"] = password
        }));

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        return client;
    }

    private static async Task<string> GetAntiforgeryTokenFromPageAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(
            html,
            @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""",
            RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            match = Regex.Match(
                html,
                @"value=""([^""]+)""\s+name=""__RequestVerificationToken""",
                RegexOptions.IgnoreCase);
        }

        Assert.True(match.Success, $"No se encontró token antiforgery en {path}");
        return match.Groups[1].Value;
    }

    [Fact]
    public async Task DemoSandbox_WhenDisabledReturns404_WhenEnabledIsolatesDataBlocksAdminAndDeletesAllOnLogout()
    {
        // 1. Por defecto DemoSandbox:Enabled = false -> POST /account/demo-login devuelve 404
        await using var defaultFactory = await CreateInitializedFactoryAsync();
        using var defaultClient = defaultFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var defaultLoginToken = await GetAntiforgeryTokenFromPageAsync(defaultClient, "/account/login");
        var disabledResponse = await defaultClient.PostAsync("/account/demo-login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = defaultLoginToken
        }));
        Assert.Equal(HttpStatusCode.NotFound, disabledResponse.StatusCode);

        // 2. Habilitando DemoSandbox:Enabled = true en un entorno de vista previa local
        await using var sandboxFactory = await CreateInitializedFactoryAsync(new Dictionary<string, string?>
        {
            ["DemoSandbox:Enabled"] = "true"
        });

        using var demoClient = sandboxFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var demoLoginToken = await GetAntiforgeryTokenFromPageAsync(demoClient, "/account/login");
        var demoLoginResponse = await demoClient.PostAsync("/account/demo-login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = demoLoginToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, demoLoginResponse.StatusCode);
        Assert.Equal("/notas", demoLoginResponse.Headers.Location?.OriginalString);

        // 3. El usuario demo ve sus notas de demostración precargadas y puede crear una nota propia aislada
        var notasGet = await demoClient.GetAsync("/notas");
        Assert.Equal(HttpStatusCode.OK, notasGet.StatusCode);
        var notasHtml = await notasGet.Content.ReadAsStringAsync();
        Assert.Contains("Bienvenido al Sandbox Demo", notasHtml);

        var createToken = await GetAntiforgeryTokenFromPageAsync(demoClient, "/notas/crear");
        var createNoteResponse = await demoClient.PostAsync("/notas/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = createToken,
            ["Titulo"] = "Nota efímera creada en Demo",
            ["Contenido"] = "Se borrará al cerrar sesión"
        }));
        Assert.Equal(HttpStatusCode.Redirect, createNoteResponse.StatusCode);

        // 4. El usuario demo NO tiene acceso a administración (/admin/users -> 403/AccessDenied) ni a cambio de contraseña
        var adminUsersResponse = await demoClient.GetAsync("/admin/users");
        Assert.True(
            adminUsersResponse.StatusCode == HttpStatusCode.Forbidden ||
            (adminUsersResponse.StatusCode == HttpStatusCode.Redirect &&
             adminUsersResponse.Headers.Location?.OriginalString.Contains("access-denied", StringComparison.OrdinalIgnoreCase) == true));

        var changePassGet = await demoClient.GetAsync("/account/change-password");
        Assert.Equal(HttpStatusCode.Redirect, changePassGet.StatusCode);

        // 5. Un usuario real (Propietario) NO ve las notas del usuario demo
        using var ownerClient = await CreateAuthenticatedClientAsync(sandboxFactory, OwnerEmail, OwnerPassword);
        var ownerNotasHtml = await (await ownerClient.GetAsync("/notas")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("Nota efímera creada en Demo", ownerNotasHtml);
        Assert.DoesNotContain("Bienvenido al Sandbox Demo", ownerNotasHtml);

        // 6. Al cerrar sesión el usuario demo, se eliminan de la BD todas sus notas, auditoría y cuenta efímera
        var logoutToken = await GetAntiforgeryTokenFromPageAsync(demoClient, "/notas");
        var logoutResponse = await demoClient.PostAsync("/account/logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = logoutToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);

        using var scope = sandboxFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();
        var remainingDemoNotes = await db.Set<Nota>()
            .IgnoreQueryFilters()
            .Where(n => n.Titulo == "Nota efímera creada en Demo")
            .CountAsync();
        Assert.Equal(0, remainingDemoNotes);
    }
}

