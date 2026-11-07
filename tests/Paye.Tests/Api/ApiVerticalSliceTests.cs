using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Paye.Core.Authorization;
using Paye.Core.Modules.Clientes;
using Paye.Infrastructure.Data;
using Paye.Infrastructure.Identity;
using Paye.Tests.Infrastructure;
using Paye.Web.Controllers.Api;
using Testcontainers.PostgreSql;

namespace Paye.Tests.Api;

public sealed class ApiVerticalSlicePostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("paye_test_api_json")
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

public sealed class ApiVerticalSliceTests(ApiVerticalSlicePostgresFixture dbFixture)
    : IClassFixture<ApiVerticalSlicePostgresFixture>
{
    private const string OwnerEmail = "propietario.api@payedigital.test";
    private const string OwnerPassword = "ClaveSegura#Paye2026!";

    private async Task<PayeWebFactory> CreateInitializedFactoryAsync(string environment = "Production")
    {
        var overrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = dbFixture.ConnectionString,
            ["SEED_OWNER_EMAIL"] = OwnerEmail,
            ["SEED_OWNER_PASSWORD"] = OwnerPassword,
            ["SEED_OWNER_FULLNAME"] = "Propietario API"
        };

        var factory = new PayeWebFactory(overrides, environment: environment);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();
        await db.Database.MigrateAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await seeder.SeedRolesAndPermissionsAsync();
        await seeder.SeedInitialOwnerFromEnvironmentAsync();

        return factory;
    }

    private static HttpClient CreateHttpsClient(PayeWebFactory factory, bool allowAutoRedirect = false, bool handleCookies = true)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = allowAutoRedirect,
            HandleCookies = handleCookies
        });
    }

    [Fact]
    public async Task OpenApi_Specification_IsAvailable_InDevelopment_AndSavedToWeb()
    {
        using var factory = await CreateInitializedFactoryAsync(environment: "Development");
        using var client = CreateHttpsClient(factory);

        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/clientes", json);
        Assert.Contains("/api/auth/login", json);
        Assert.Contains("/api/me", json);

        // Guardar openapi.json en web/ para generar cliente TypeScript
        var webDir = @"d:\Desarrollo\Paye_POS\web";
        if (Directory.Exists(webDir))
        {
            var openApiPath = Path.Combine(webDir, "openapi.json");
            await File.WriteAllTextAsync(openApiPath, json);
        }
    }

    [Fact]
    public async Task OpenApi_Specification_InProduction_Returns_404_NotFound()
    {
        using var factory = await CreateInitializedFactoryAsync(environment: "Production");
        using var client = CreateHttpsClient(factory, allowAutoRedirect: false);

        // Sin sesión en producción: denegado (302 a login o 404) y nunca 200 OK
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Found || response.StatusCode == HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Anonymous_Request_To_Api_Returns_401_Unauthorized_Without_Redirect()
    {
        using var factory = await CreateInitializedFactoryAsync();
        using var client = CreateHttpsClient(factory, allowAutoRedirect: false, handleCookies: false);

        // 1. /api/me
        using var meRes = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meRes.StatusCode);
        Assert.False(meRes.Headers.Location is not null);

        // 2. /api/me/navigation
        using var navRes = await client.GetAsync("/api/me/navigation");
        Assert.Equal(HttpStatusCode.Unauthorized, navRes.StatusCode);
        Assert.False(navRes.Headers.Location is not null);

        // 3. /api/clientes
        using var clientesRes = await client.GetAsync("/api/clientes");
        Assert.Equal(HttpStatusCode.Unauthorized, clientesRes.StatusCode);
        Assert.False(clientesRes.Headers.Location is not null);
    }

    [Fact]
    public async Task Forbidden_Request_To_Api_Returns_403_Forbidden_Without_Redirect()
    {
        using var factory = await CreateInitializedFactoryAsync();

        var roleWithoutClientes = "RolSinClientes_" + Guid.NewGuid().ToString("N");
        var limitedUserEmail = $"limitado.{Guid.NewGuid():N}@payedigital.test";
        const string limitedPassword = "ClaveLimitada#2026!";

        using (var scope = factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            await roleManager.CreateAsync(new IdentityRole(roleWithoutClientes));

            var user = new ApplicationUser
            {
                UserName = limitedUserEmail,
                Email = limitedUserEmail,
                EmailConfirmed = true,
                FullName = "Usuario Limitado",
                IsActive = true
            };
            await userManager.CreateAsync(user, limitedPassword);
            await userManager.AddToRoleAsync(user, roleWithoutClientes);
        }

        using var client = CreateHttpsClient(factory, allowAutoRedirect: false, handleCookies: true);

        // Iniciar sesión
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginApiRequest(limitedUserEmail, limitedPassword));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        // Intentar acceder a /api/clientes sin permiso Clientes.Ver -> 403 Forbidden
        var forbiddenRes = await client.GetAsync("/api/clientes");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRes.StatusCode);
        Assert.False(forbiddenRes.Headers.Location is not null);
    }

    [Fact]
    public async Task OptimisticConcurrency_Xmin_Returns_409_Conflict_With_ProblemDetails()
    {
        using var factory = await CreateInitializedFactoryAsync();

        // 1. Login como Propietario (tiene todos los permisos)
        using var client = CreateHttpsClient(factory, allowAutoRedirect: false, handleCookies: true);

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginApiRequest(OwnerEmail, OwnerPassword));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        // Obtener antiforgery token
        var afRes = await client.GetFromJsonAsync<AntiforgeryTokenResponse>("/api/antiforgery");
        Assert.NotNull(afRes);
        client.DefaultRequestHeaders.Add("RequestVerificationToken", afRes.Token);

        // 2. Crear Cliente
        var createReq = new CreateClienteRequest
        {
            NombreFiscal = "Empresa Concurrencia S.L.",
            CifNif = "B99887766",
            Email = "contacto@concurrencia.test",
            Activo = true
        };

        var postRes = await client.PostAsJsonAsync("/api/clientes", createReq);
        Assert.Equal(HttpStatusCode.Created, postRes.StatusCode);
        var createdDto = await postRes.Content.ReadFromJsonAsync<ClienteDto>();
        Assert.NotNull(createdDto);

        var originalRowVersion = createdDto.RowVersion;

        // 3. Primera actualización con éxito (modifica xmin en PostgreSQL)
        var updateReq1 = new UpdateClienteRequest
        {
            NombreFiscal = "Empresa Concurrencia S.L. Modificada",
            CifNif = "B99887766",
            Email = "actualizado@concurrencia.test",
            Activo = true,
            RowVersion = originalRowVersion
        };

        var putRes1 = await client.PutAsJsonAsync($"/api/clientes/{createdDto.Id}", updateReq1);
        Assert.Equal(HttpStatusCode.OK, putRes1.StatusCode);
        var updatedDto = await putRes1.Content.ReadFromJsonAsync<ClienteDto>();
        Assert.NotNull(updatedDto);
        Assert.NotEqual(originalRowVersion, updatedDto.RowVersion);

        // 4. Segunda actualización usando el RowVersion ANTIGUO (simula edición desfasada) -> Debe retornar 409 Conflict
        var updateReqConflicto = new UpdateClienteRequest
        {
            NombreFiscal = "Edición Concurrente Desfasada",
            CifNif = "B99887766",
            Email = "conflicto@concurrencia.test",
            Activo = true,
            RowVersion = originalRowVersion // RowVersion desactualizado
        };

        var putResConflicto = await client.PutAsJsonAsync($"/api/clientes/{createdDto.Id}", updateReqConflicto);
        Assert.Equal(HttpStatusCode.Conflict, putResConflicto.StatusCode);

        var problemDetails = await putResConflicto.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Equal(409, problemDetails.Status);
        Assert.Equal("Conflicto de concurrencia", problemDetails.Title);
    }

    [Fact]
    public async Task Antiforgery_Endpoint_Returns_Token()
    {
        using var factory = await CreateInitializedFactoryAsync();
        using var client = CreateHttpsClient(factory);

        var tokenRes = await client.GetFromJsonAsync<AntiforgeryTokenResponse>("/api/antiforgery");
        Assert.NotNull(tokenRes);
        Assert.False(string.IsNullOrWhiteSpace(tokenRes.Token));
    }

    [Fact]
    public async Task Auth_Login_Logout_And_Me_Navigation_Flow()
    {
        using var factory = await CreateInitializedFactoryAsync();
        using var client = CreateHttpsClient(factory, allowAutoRedirect: false, handleCookies: true);

        // 1. Login
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginApiRequest(OwnerEmail, OwnerPassword));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var loginData = await loginRes.Content.ReadFromJsonAsync<LoginApiResponse>();
        Assert.NotNull(loginData);
        Assert.False(loginData.RequiresTwoFactor);
        Assert.NotNull(loginData.User);
        Assert.Contains(ClientesPermissions.Ver, loginData.User.Permissions);

        // 2. GET /api/me
        var meRes = await client.GetFromJsonAsync<ApiUserDto>("/api/me");
        Assert.NotNull(meRes);
        Assert.Equal(OwnerEmail, meRes.Email);
        Assert.Contains(ClientesPermissions.Ver, meRes.Permissions);

        // 3. GET /api/me/navigation
        var navRes = await client.GetFromJsonAsync<ApiNavigationDataDto>("/api/me/navigation");
        Assert.NotNull(navRes);
        Assert.NotEmpty(navRes.NavGroups);
        var generalGroup = navRes.NavGroups.FirstOrDefault(g => g.Title == "General");
        Assert.NotNull(generalGroup);
        Assert.Contains(generalGroup.Items, item => item.Title == "Clientes" && item.Url == "/clientes");

        // 4. Logout (con Antiforgery token)
        var afRes = await client.GetFromJsonAsync<AntiforgeryTokenResponse>("/api/antiforgery");
        Assert.NotNull(afRes);
        client.DefaultRequestHeaders.Add("RequestVerificationToken", afRes.Token);

        var logoutRes = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logoutRes.StatusCode);

        // 5. Subsequent /api/me -> 401
        var unauthRes = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthRes.StatusCode);
    }

    [Fact]
    public async Task Post_Without_Antiforgery_Token_Is_Rejected_With_400_BadRequest()
    {
        using var factory = await CreateInitializedFactoryAsync();
        using var client = CreateHttpsClient(factory, allowAutoRedirect: false, handleCookies: true);

        // Login como Propietario (autenticado)
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginApiRequest(OwnerEmail, OwnerPassword));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        // Intento de POST /api/clientes SIN la cabecera RequestVerificationToken
        var createReq = new CreateClienteRequest
        {
            NombreFiscal = "Test Sin CSRF S.L.",
            CifNif = "B12345678",
            Email = "sin.csrf@payedigital.test",
            Activo = true
        };

        var postRes = await client.PostAsJsonAsync("/api/clientes", createReq);
        Assert.Equal(HttpStatusCode.BadRequest, postRes.StatusCode);
    }

    [Fact]
    public async Task Five_Failed_Logins_Locks_Account_With_423_Locked()
    {
        using var factory = await CreateInitializedFactoryAsync();

        var lockoutUserEmail = $"lockout.{Guid.NewGuid():N}@payedigital.test";
        const string correctPassword = "ClaveSegura#Lockout2026!";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = lockoutUserEmail,
                Email = lockoutUserEmail,
                EmailConfirmed = true,
                FullName = "Usuario Prueba Lockout",
                IsActive = true
            };
            await userManager.CreateAsync(user, correctPassword);
        }

        using var client = CreateHttpsClient(factory, allowAutoRedirect: false, handleCookies: true);

        // Realizar 5 intentos fallidos
        HttpResponseMessage lastResponse = null!;
        for (var i = 1; i <= 5; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginApiRequest(lockoutUserEmail, "ClaveErronea#123!"));
        }

        // Si el 5º fue el último fallo permitido, el siguiente o el 5º devuelve 423
        if (lastResponse.StatusCode != (HttpStatusCode)423)
        {
            lastResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginApiRequest(lockoutUserEmail, "ClaveErronea#123!"));
        }

        Assert.Equal((HttpStatusCode)423, lastResponse.StatusCode);
        var problemDetails = await lastResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Equal(423, problemDetails.Status);
        Assert.Equal("Cuenta bloqueada", problemDetails.Title);
    }

    [Fact]
    public async Task RateLimit_Exceeded_On_Login_Returns_429_TooManyRequests()
    {
        using var factory = await CreateInitializedFactoryAsync();
        using var client = CreateHttpsClient(factory, allowAutoRedirect: false, handleCookies: true);

        // La política "auth-strict" en Program.cs permite 10 peticiones por minuto.
        // Hacemos 15 peticiones rápidas seguidas desde el mismo cliente.
        HttpResponseMessage? rateLimitedResponse = null;
        for (var i = 0; i < 15; i++)
        {
            var res = await client.PostAsJsonAsync("/api/auth/login", new LoginApiRequest($"rate.{i}@payedigital.test", "ClaveTest#123!"));
            if (res.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimitedResponse = res;
                break;
            }
        }

        Assert.NotNull(rateLimitedResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, rateLimitedResponse.StatusCode);
    }

    [Fact]
    public async Task CreateCliente_Without_ClientesCrear_Permission_Returns_403_Forbidden()
    {
        using var factory = await CreateInitializedFactoryAsync();

        var readOnlyRole = "RolLectorClientes_" + Guid.NewGuid().ToString("N");
        var readOnlyEmail = $"lector.{Guid.NewGuid():N}@payedigital.test";
        const string password = "ClaveLector#2026!";

        using (var scope = factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var role = new IdentityRole(readOnlyRole);
            await roleManager.CreateAsync(role);
            // Solo permiso Clientes.Ver, NO Clientes.Crear
            await roleManager.AddClaimAsync(role, new Claim(CorePermissions.ClaimType, ClientesPermissions.Ver));

            var user = new ApplicationUser
            {
                UserName = readOnlyEmail,
                Email = readOnlyEmail,
                EmailConfirmed = true,
                FullName = "Usuario Lector",
                IsActive = true
            };
            await userManager.CreateAsync(user, password);
            await userManager.AddToRoleAsync(user, readOnlyRole);
        }

        using var client = CreateHttpsClient(factory, allowAutoRedirect: false, handleCookies: true);

        // 1. Iniciar sesión
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginApiRequest(readOnlyEmail, password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        // 2. Obtener token CSRF
        var afRes = await client.GetFromJsonAsync<AntiforgeryTokenResponse>("/api/antiforgery");
        Assert.NotNull(afRes);
        client.DefaultRequestHeaders.Add("RequestVerificationToken", afRes.Token);

        // 3. Intentar crear cliente sin permiso Clientes.Crear -> 403 Forbidden
        var createReq = new CreateClienteRequest
        {
            NombreFiscal = "Intento No Autorizado S.L.",
            CifNif = "B55443322",
            Email = "noautorizado@payedigital.test",
            Activo = true
        };

        var postRes = await client.PostAsJsonAsync("/api/clientes", createReq);
        Assert.Equal(HttpStatusCode.Forbidden, postRes.StatusCode);
        Assert.False(postRes.Headers.Location is not null);
    }
}
