using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Paye.Core.Authorization;
using Paye.Core.Modules.Notas;
using Paye.Infrastructure.Data;
using Paye.Infrastructure.Identity;
using Paye.Tests.Infrastructure;
using Testcontainers.PostgreSql;

namespace Paye.Tests.Milestone3b;

public sealed class Milestone3bPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("paye_test_m3b")
        .WithUsername("paye_test_user")
        .WithPassword("TestOnly_NotProd_987!")
        .Build();

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

public sealed class NotasModuleAndNavigationTests(Milestone3bPostgresFixture dbFixture)
    : IClassFixture<Milestone3bPostgresFixture>
{
    private const string OwnerEmail = "propietario.m3b@payedigital.test";
    private const string OwnerPassword = "ClaveSegura#Paye2026!";

    private async Task<PayeWebFactory> CreateInitializedFactoryAsync()
    {
        var storageDir = Path.Combine(
            Path.GetTempPath(),
            "paye-test-private-files-m3b-" + Guid.NewGuid().ToString("N"));

        var overrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = dbFixture.ConnectionString,
            ["Security:PrivateUploadsPath"] = storageDir,
            ["SEED_OWNER_EMAIL"] = OwnerEmail,
            ["SEED_OWNER_PASSWORD"] = OwnerPassword,
            ["SEED_OWNER_FULLNAME"] = "Propietario M3b"
        };

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
    public async Task SidebarNavigation_RendersModuleLinks_FilteredByUserPermissions()
    {
        using var factory = await CreateInitializedFactoryAsync();

        var empleadoEmail = $"empleado.nav.{Guid.NewGuid():N}@payedigital.test";
        var sinNotasEmail = $"sinnotas.nav.{Guid.NewGuid():N}@payedigital.test";
        const string userPassword = "ClaveEmpleado#2026!";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // 1. Create Empleado user (has Notas.Ver, lacks Usuarios.Ver & Auditoria.Ver)
            var empleado = new ApplicationUser
            {
                UserName = empleadoEmail,
                Email = empleadoEmail,
                EmailConfirmed = true,
                FullName = "Empleado Navegación",
                IsActive = true
            };
            Assert.True((await userManager.CreateAsync(empleado, userPassword)).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(empleado, SystemRoles.Empleado)).Succeeded);

            // 2. Create custom role without Notas.Ver
            const string customRole = "SoloInicio";
            if (!await roleManager.RoleExistsAsync(customRole))
            {
                Assert.True((await roleManager.CreateAsync(new IdentityRole(customRole))).Succeeded);
            }

            var sinNotasUser = new ApplicationUser
            {
                UserName = sinNotasEmail,
                Email = sinNotasEmail,
                EmailConfirmed = true,
                FullName = "Usuario Sin Notas",
                IsActive = true
            };
            Assert.True((await userManager.CreateAsync(sinNotasUser, userPassword)).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(sinNotasUser, customRole)).Succeeded);
        }

        // Propietario sees /notas, /admin/users, and /admin/audit in sidebar
        using var ownerClient = await CreateAuthenticatedClientAsync(factory, OwnerEmail, OwnerPassword);
        var ownerHomeResponse = await ownerClient.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, ownerHomeResponse.StatusCode);
        var ownerHtml = await ownerHomeResponse.Content.ReadAsStringAsync();
        Assert.Contains("href=\"/notas\"", ownerHtml);
        Assert.Contains("href=\"/admin/users\"", ownerHtml);
        Assert.Contains("href=\"/admin/audit\"", ownerHtml);

        // Empleado sees /notas in sidebar, but NOT /admin/users or /admin/audit
        using var empleadoClient = await CreateAuthenticatedClientAsync(factory, empleadoEmail, userPassword);
        var empleadoHomeResponse = await empleadoClient.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, empleadoHomeResponse.StatusCode);
        var empleadoHtml = await empleadoHomeResponse.Content.ReadAsStringAsync();
        Assert.Contains("href=\"/notas\"", empleadoHtml);
        Assert.DoesNotContain("href=\"/admin/users\"", empleadoHtml);
        Assert.DoesNotContain("href=\"/admin/audit\"", empleadoHtml);

        // Usuario sin permiso Notas.Ver does NOT see /notas in sidebar and is denied on GET /notas
        using var sinNotasClient = await CreateAuthenticatedClientAsync(factory, sinNotasEmail, userPassword);
        var sinNotasHomeResponse = await sinNotasClient.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, sinNotasHomeResponse.StatusCode);
        var sinNotasHtml = await sinNotasHomeResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("href=\"/notas\"", sinNotasHtml);

        var forbiddenNotasResponse = await sinNotasClient.GetAsync("/notas");
        Assert.Equal(HttpStatusCode.Redirect, forbiddenNotasResponse.StatusCode);
        Assert.Contains("/account/access-denied", forbiddenNotasResponse.Headers.Location?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task NotasModule_Crud_Search_Filter_And_AuditTrail_WorkAgainstPostgreSql()
    {
        using var factory = await CreateInitializedFactoryAsync();
        using var ownerClient = await CreateAuthenticatedClientAsync(factory, OwnerEmail, OwnerPassword);

        var uniqueTag = Guid.NewGuid().ToString("N")[..8];
        var noteTitle = $"Aviso proveedor {uniqueTag}";
        var noteContent = $"Revisar albarán pendiente del lote {uniqueTag}.";

        // 1. Create note via POST /notas/crear
        var createToken = await GetAntiforgeryTokenFromPageAsync(ownerClient, "/notas/crear");
        var createResponse = await ownerClient.PostAsync("/notas/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = createToken,
            ["Titulo"] = noteTitle,
            ["Contenido"] = noteContent
        }));

        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        Assert.Equal("/notas", createResponse.Headers.Location?.ToString());

        Guid createdNotaId;
        uint initialRowVersion;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();
            var nota = await db.Set<Nota>().SingleAsync(n => n.Titulo == noteTitle);
            createdNotaId = nota.Id;
            initialRowVersion = nota.RowVersion;

            Assert.Equal(noteContent, nota.Contenido);
            Assert.False(nota.Archivada);
            Assert.NotNull(nota.CreatedBy);
            Assert.True(nota.RowVersion > 0);

            var createAuditExists = await db.AuditLogs.AnyAsync(
                a => a.Action == "Notas.Create" && a.EntityId == createdNotaId.ToString());
            Assert.True(createAuditExists);
        }

        // 2. Search by query (?q=uniqueTag) finds the created note
        var searchResponse = await ownerClient.GetAsync($"/notas?q={uniqueTag}");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        var searchHtml = await searchResponse.Content.ReadAsStringAsync();
        Assert.Contains(noteTitle, searchHtml);

        // 3. Edit note and mark as archived via POST /notas/{id}/editar
        var editPath = $"/notas/{createdNotaId}/editar";
        var editToken = await GetAntiforgeryTokenFromPageAsync(ownerClient, editPath);
        var updatedTitle = $"{noteTitle} (Verificado)";
        var updatedContent = $"{noteContent} Confirmado en almacén.";

        var editResponse = await ownerClient.PostAsync(editPath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = editToken,
            ["Id"] = createdNotaId.ToString(),
            ["Titulo"] = updatedTitle,
            ["Contenido"] = updatedContent,
            ["Archivada"] = "true",
            ["RowVersion"] = initialRowVersion.ToString(CultureInfo.InvariantCulture)
        }));

        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

        // 4. Active filter (?estado=activas) hides archived note; archived filter (?estado=archivadas) shows it
        var activeListHtml = await (await ownerClient.GetAsync($"/notas?q={uniqueTag}&estado=activas")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(updatedTitle, activeListHtml);

        var archivedListHtml = await (await ownerClient.GetAsync($"/notas?q={uniqueTag}&estado=archivadas")).Content.ReadAsStringAsync();
        Assert.Contains(updatedTitle, archivedListHtml);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();
            var updatedNota = await db.Set<Nota>().SingleAsync(n => n.Id == createdNotaId);
            Assert.True(updatedNota.Archivada);
            Assert.NotEqual(initialRowVersion, updatedNota.RowVersion);

            var updateAuditExists = await db.AuditLogs.AnyAsync(
                a => a.Action == "Notas.Update" && a.EntityId == createdNotaId.ToString());
            Assert.True(updateAuditExists);
        }
    }

    [Fact]
    public async Task NotasModule_EditWithStaleRowVersion_DetectsConcurrencyConflictInUi()
    {
        using var factory = await CreateInitializedFactoryAsync();
        using var ownerClient = await CreateAuthenticatedClientAsync(factory, OwnerEmail, OwnerPassword);

        var uniqueTag = Guid.NewGuid().ToString("N")[..8];
        var createToken = await GetAntiforgeryTokenFromPageAsync(ownerClient, "/notas/crear");
        await ownerClient.PostAsync("/notas/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = createToken,
            ["Titulo"] = $"Nota Concurrencia {uniqueTag}",
            ["Contenido"] = "Contenido versión 1"
        }));

        Guid notaId;
        uint staleRowVersion;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();
            var nota = await db.Set<Nota>().SingleAsync(n => n.Titulo == $"Nota Concurrencia {uniqueTag}");
            notaId = nota.Id;
            staleRowVersion = nota.RowVersion;

            // Simulate another user modifying the same note concurrently in DB
            nota.Contenido = "Contenido modificado por otro usuario (versión 2)";
            await db.SaveChangesAsync();
        }

        // Submit edit form using stale RowVersion
        var editPath = $"/notas/{notaId}/editar";
        var editToken = await GetAntiforgeryTokenFromPageAsync(ownerClient, editPath);
        var conflictResponse = await ownerClient.PostAsync(editPath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = editToken,
            ["Id"] = notaId.ToString(),
            ["Titulo"] = $"Nota Concurrencia {uniqueTag} Sobrescrita",
            ["Contenido"] = "Intento de sobrescritura con versión antigua",
            ["Archivada"] = "false",
            ["RowVersion"] = staleRowVersion.ToString(CultureInfo.InvariantCulture)
        }));

        // Returns 200 OK with concurrency warning and preserves version 2 in DB
        Assert.Equal(HttpStatusCode.OK, conflictResponse.StatusCode);
        var conflictHtml = await conflictResponse.Content.ReadAsStringAsync();
        Assert.Contains("La nota fue modificada por otro usuario", conflictHtml);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();
            var currentNota = await db.Set<Nota>().AsNoTracking().SingleAsync(n => n.Id == notaId);
            Assert.Equal("Contenido modificado por otro usuario (versión 2)", currentNota.Contenido);
        }
    }

    [Fact]
    public async Task NotasModule_SoftDelete_EnforcesNotasBorrarPermission_And_AppliesGlobalFilter()
    {
        using var factory = await CreateInitializedFactoryAsync();
        using var ownerClient = await CreateAuthenticatedClientAsync(factory, OwnerEmail, OwnerPassword);

        var empleadoEmail = $"empleado.del.{Guid.NewGuid():N}@payedigital.test";
        const string empleadoPassword = "ClaveEmpleado#2026!";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var empleado = new ApplicationUser
            {
                UserName = empleadoEmail,
                Email = empleadoEmail,
                EmailConfirmed = true,
                FullName = "Empleado Sin Borrado",
                IsActive = true
            };
            Assert.True((await userManager.CreateAsync(empleado, empleadoPassword)).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(empleado, SystemRoles.Empleado)).Succeeded);
        }

        var uniqueTag = Guid.NewGuid().ToString("N")[..8];
        var createToken = await GetAntiforgeryTokenFromPageAsync(ownerClient, "/notas/crear");
        await ownerClient.PostAsync("/notas/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = createToken,
            ["Titulo"] = $"Nota Borrado {uniqueTag}",
            ["Contenido"] = "Nota para verificar borrado lógico y permisos"
        }));

        Guid notaId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();
            notaId = (await db.Set<Nota>().SingleAsync(n => n.Titulo == $"Nota Borrado {uniqueTag}")).Id;
        }

        // 1. Empleado (lacks Notas.Borrar) is denied on GET /notas/{id}/borrar
        using var empleadoClient = await CreateAuthenticatedClientAsync(factory, empleadoEmail, empleadoPassword);
        var empleadoDeleteGet = await empleadoClient.GetAsync($"/notas/{notaId}/borrar");
        Assert.Equal(HttpStatusCode.Redirect, empleadoDeleteGet.StatusCode);
        Assert.Contains("/account/access-denied", empleadoDeleteGet.Headers.Location?.ToString() ?? string.Empty);

        // 2. Propietario (has Notas.Borrar) deletes note via POST /notas/{id}/borrar
        var deletePath = $"/notas/{notaId}/borrar";
        var deleteToken = await GetAntiforgeryTokenFromPageAsync(ownerClient, deletePath);
        var deleteResponse = await ownerClient.PostAsync(deletePath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = deleteToken
        }));

        Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);
        Assert.Equal("/notas", deleteResponse.Headers.Location?.ToString());

        // 3. Verify soft-deleted note is filtered out from normal queries, retained with IsDeleted=true, and audited
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();

            var visibleNota = await db.Set<Nota>().FirstOrDefaultAsync(n => n.Id == notaId);
            Assert.Null(visibleNota);

            var deletedNota = await db.Set<Nota>()
                .IgnoreQueryFilters()
                .SingleAsync(n => n.Id == notaId);

            Assert.True(deletedNota.IsDeleted);
            Assert.NotNull(deletedNota.DeletedAtUtc);
            Assert.NotNull(deletedNota.DeletedBy);

            var deleteAuditExists = await db.AuditLogs.AnyAsync(
                a => a.Action == "Notas.Delete" && a.EntityId == notaId.ToString());
            Assert.True(deleteAuditExists);
        }
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(
        PayeWebFactory factory,
        string email,
        string password)
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
}
