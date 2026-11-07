using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paye.Core.Authorization;
using Paye.Core.Demo;
using Paye.Core.Invoicing;
using Paye.Core.Modules.Facturacion;
using Paye.Core.Navigation;
using Paye.Infrastructure.Data;
using Paye.Infrastructure.Identity;
using Paye.Infrastructure.Modules.Facturacion;
using Paye.Tests.Infrastructure;
using Paye.Web.Modules.Facturacion;
using Testcontainers.PostgreSql;

namespace Paye.Tests.Facturacion;

public sealed class FacturacionPaso3PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("paye_test_facturacion_p3")
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

public sealed class FacturacionPaso3WebAndModularityTests(FacturacionPaso3PostgresFixture dbFixture)
    : IClassFixture<FacturacionPaso3PostgresFixture>
{
    private const string OwnerEmail = "propietario.factp3@payedigital.test";
    private const string OwnerPassword = "ClaveSegura#Paye2026!";

    private async Task<PayeWebFactory> CreateInitializedFactoryAsync(
        IDictionary<string, string?>? extraOverrides = null)
    {
        var storageDir = Path.Combine(
            Path.GetTempPath(),
            "paye-test-private-files-factp3-" + Guid.NewGuid().ToString("N"));

        var overrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = dbFixture.ConnectionString,
            ["Security:PrivateUploadsPath"] = storageDir,
            ["SEED_OWNER_EMAIL"] = OwnerEmail,
            ["SEED_OWNER_PASSWORD"] = OwnerPassword,
            ["SEED_OWNER_FULLNAME"] = "Propietario Facturación P3"
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
    public void Architecture_FacturacionDoesNotReferencePos_And_CoreAndDbContextRemainDecoupled()
    {
        // 1. Verify PayeDbContext has zero public DbSet or properties referencing Paye.Core.Modules.Facturacion
        var dbContextProps = typeof(PayeDbContext).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.DoesNotContain(dbContextProps, p =>
            p.PropertyType.FullName?.Contains("Modules.Facturacion", StringComparison.Ordinal) == true);

        // 2. Verify all Facturacion types across Core, Infrastructure, and Web have zero references to Pos
        var assemblies = new[]
        {
            typeof(Invoice).Assembly,
            typeof(FacturacionService).Assembly,
            typeof(FacturacionController).Assembly
        };

        var facturacionTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t =>
                t.Namespace?.Contains("Modules.Facturacion", StringComparison.Ordinal) == true ||
                t.FullName?.Contains("Modules_Facturacion_", StringComparison.Ordinal) == true)
            .ToList();

        Assert.NotEmpty(facturacionTypes);

        foreach (var type in facturacionTypes)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                Assert.False(
                    field.FieldType.FullName?.Contains(".Pos", StringComparison.OrdinalIgnoreCase) == true,
                    $"El tipo {type.FullName} referencia a Pos en el campo {field.Name} ({field.FieldType.FullName}).");
            }

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                Assert.False(
                    prop.PropertyType.FullName?.Contains(".Pos", StringComparison.OrdinalIgnoreCase) == true,
                    $"El tipo {type.FullName} referencia a Pos en la propiedad {prop.Name} ({prop.PropertyType.FullName}).");
            }

            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                foreach (var param in ctor.GetParameters())
                {
                    Assert.False(
                        param.ParameterType.FullName?.Contains(".Pos", StringComparison.OrdinalIgnoreCase) == true,
                        $"El constructor de {type.FullName} referencia a Pos en el parámetro {param.Name} ({param.ParameterType.FullName}).");
                }
            }
        }

        // 3. Verify non-module types in Paye.Core and Paye.Infrastructure do not couple to Modules.Facturacion
        foreach (var assembly in new[] { typeof(Invoice).Assembly, typeof(PayeDbContext).Assembly })
        {
            var nonModuleTypes = assembly.GetTypes()
                .Where(t =>
                    t.Namespace is not null &&
                    !t.Namespace.Contains(".Modules.", StringComparison.Ordinal) &&
                    !t.Namespace.Contains(".Migrations", StringComparison.Ordinal));

            foreach (var type in nonModuleTypes)
            {
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    Assert.False(
                        field.FieldType.Namespace?.Contains(".Modules.Facturacion", StringComparison.Ordinal) == true,
                        $"El tipo base {type.FullName} tiene un campo acoplado al módulo Facturacion ({field.FieldType.FullName}).");
                }
            }
        }
    }

    [Fact]
    public async Task AppStartsAndRunsWithoutFacturacionModule_SidebarExcludesFacturacionLink()
    {
        using var baseFactory = await CreateInitializedFactoryAsync();
        using var factoryWithoutFacturacion = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var navDescriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(IModuleNavigationProvider) &&
                         d.ImplementationType == typeof(FacturacionNavigationProvider));
                if (navDescriptor is not null)
                {
                    services.Remove(navDescriptor);
                }

                services.RemoveAll<IFacturacionService>();
                services.RemoveAll<IInvoiceIssuer>();
                services.RemoveAll<ITaxCatalogReader>();
                services.RemoveAll<FacturacionService>();
            });
        });

        using var client = await CreateAuthenticatedClientAsync(factoryWithoutFacturacion, OwnerEmail, OwnerPassword);
        var homeResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);

        var homeHtml = await homeResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("href=\"/facturacion\"", homeHtml);
        Assert.Contains("href=\"/notas\"", homeHtml);
        Assert.Contains("href=\"/admin/users\"", homeHtml);
    }

    [Fact]
    public async Task FacturacionWeb_DenyByDefault_GranularPermissions_Antiforgery_And_StrictCspHeaders()
    {
        using var factory = await CreateInitializedFactoryAsync();

        // 1. Unauthenticated GET /facturacion redirects to /account/login
        using var anonClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var anonGet = await anonClient.GetAsync("/facturacion");
        Assert.Equal(HttpStatusCode.Redirect, anonGet.StatusCode);
        Assert.Contains("/account/login", anonGet.Headers.Location?.ToString() ?? string.Empty);

        // 2. Create Empleado user (has Ver, Crear, Emitir, Descargar; lacks Rectificar & Configurar)
        //    and a custom role without Facturacion.Ver
        var empleadoEmail = $"empleado.factp3.{Guid.NewGuid():N}@payedigital.test";
        var sinFactEmail = $"sinfact.factp3.{Guid.NewGuid():N}@payedigital.test";
        const string userPassword = "ClaveEmpleado#2026!";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            var empleado = new ApplicationUser
            {
                UserName = empleadoEmail,
                Email = empleadoEmail,
                EmailConfirmed = true,
                FullName = "Empleado Facturación",
                IsActive = true
            };
            Assert.True((await userManager.CreateAsync(empleado, userPassword)).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(empleado, SystemRoles.Empleado)).Succeeded);

            const string customRole = "SinFacturacionRole";
            if (!await roleManager.RoleExistsAsync(customRole))
            {
                Assert.True((await roleManager.CreateAsync(new IdentityRole(customRole))).Succeeded);
            }

            var sinFactUser = new ApplicationUser
            {
                UserName = sinFactEmail,
                Email = sinFactEmail,
                EmailConfirmed = true,
                FullName = "Usuario Sin Facturación",
                IsActive = true
            };
            Assert.True((await userManager.CreateAsync(sinFactUser, userPassword)).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(sinFactUser, customRole)).Succeeded);
        }

        // 3. Usuario sin Facturacion.Ver no ve el enlace en el menú lateral y recibe redirección a access-denied
        using var sinFactClient = await CreateAuthenticatedClientAsync(factory, sinFactEmail, userPassword);
        var sinFactHome = await (await sinFactClient.GetAsync("/")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("href=\"/facturacion\"", sinFactHome);

        var sinFactGet = await sinFactClient.GetAsync("/facturacion");
        Assert.Equal(HttpStatusCode.Redirect, sinFactGet.StatusCode);
        Assert.Contains("/account/access-denied", sinFactGet.Headers.Location?.ToString() ?? string.Empty);

        // 4. Empleado puede ver /facturacion, pero tiene denegado /facturacion/configuracion (Facturacion.Configurar)
        using var empleadoClient = await CreateAuthenticatedClientAsync(factory, empleadoEmail, userPassword);
        var empleadoIndex = await empleadoClient.GetAsync("/facturacion");
        Assert.Equal(HttpStatusCode.OK, empleadoIndex.StatusCode);

        var empleadoSettings = await empleadoClient.GetAsync("/facturacion/configuracion");
        Assert.Equal(HttpStatusCode.Redirect, empleadoSettings.StatusCode);
        Assert.Contains("/account/access-denied", empleadoSettings.Headers.Location?.ToString() ?? string.Empty);

        // 5. POST sin token antiforgery a /facturacion/presupuestos/crear es rechazado con 400 BadRequest
        var postWithoutAntiforgery = await empleadoClient.PostAsync(
            "/facturacion/presupuestos/crear",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["CustomerName"] = "Cliente CSRF Intento"
            }));
        Assert.Equal(HttpStatusCode.BadRequest, postWithoutAntiforgery.StatusCode);

        // 6. Verificar CSP estricta sin unsafe-inline ni unsafe-eval y HTML libre de estilos/scripts inline
        Assert.True(empleadoIndex.Headers.TryGetValues("Content-Security-Policy", out var cspValues));
        var csp = string.Join(";", cspValues);
        Assert.DoesNotContain("unsafe-inline", csp, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unsafe-eval", csp, StringComparison.OrdinalIgnoreCase);

        var indexHtml = await empleadoIndex.Content.ReadAsStringAsync();
        Assert.DoesNotContain("style=", indexHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script>", indexHtml, StringComparison.OrdinalIgnoreCase);

        // 7. Verificar endpoints JSON GET con la misma autorización por permisos
        var anonApiGet = await anonClient.GetAsync("/facturacion/api/invoices");
        Assert.Equal(HttpStatusCode.Redirect, anonApiGet.StatusCode);

        var sinFactApiGet = await sinFactClient.GetAsync("/facturacion/api/invoices");
        Assert.Equal(HttpStatusCode.Redirect, sinFactApiGet.StatusCode);
        Assert.Contains("/account/access-denied", sinFactApiGet.Headers.Location?.ToString() ?? string.Empty);

        var empleadoApiGet = await empleadoClient.GetAsync("/facturacion/api/invoices");
        Assert.Equal(HttpStatusCode.OK, empleadoApiGet.StatusCode);
        Assert.Equal("application/json", empleadoApiGet.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task FacturacionWeb_EndToEndFlow_ConfigureIssuer_CreateQuote_Convert_IssueInvoice_DownloadPdf_And_Rectify()
    {
        using var factory = await CreateInitializedFactoryAsync();
        using var ownerClient = await CreateAuthenticatedClientAsync(factory, OwnerEmail, OwnerPassword);

        // 1. Configure issuer fiscal data via POST /facturacion/configuracion/emisor
        uint settingsRowVersion;
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IFacturacionService>();
            settingsRowVersion = (await service.GetSettingsAsync()).RowVersion;
        }

        var settingsToken = await GetAntiforgeryTokenFromPageAsync(ownerClient, "/facturacion/configuracion");
        var updateIssuerResponse = await ownerClient.PostAsync(
            "/facturacion/configuracion/emisor",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = settingsToken,
                ["IssuerInput.TaxName"] = "Payé Digital S.L.",
                ["IssuerInput.TaxId"] = "B12345674",
                ["IssuerInput.FiscalAddress"] = "Calle Mayor 10, 28013 Madrid",
                ["IssuerInput.LegalTermsText"] = "Inscrita en el Registro Mercantil de Madrid.",
                ["IssuerInput.FooterText"] = "Gracias por su confianza.",
                ["IssuerInput.RowVersion"] = settingsRowVersion.ToString(CultureInfo.InvariantCulture)
            }));
        Assert.Equal(HttpStatusCode.Redirect, updateIssuerResponse.StatusCode);

        // 2. Create Quote via POST /facturacion/presupuestos/crear
        var quoteCreateToken = await GetAntiforgeryTokenFromPageAsync(ownerClient, "/facturacion/presupuestos/crear");
        var uniqueCustomer = $"Restaurante El Faro {Guid.NewGuid():N}"[..28];
        var createQuoteResponse = await ownerClient.PostAsync(
            "/facturacion/presupuestos/crear",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = quoteCreateToken,
                ["CustomerName"] = uniqueCustomer,
                ["CustomerTaxId"] = "12345678Z",
                ["CustomerAddress"] = "Avenida del Puerto 5, Valencia",
                ["Lines[0].Description"] = "Implantación TPV y formación",
                ["Lines[0].Quantity"] = "1",
                ["Lines[0].UnitPriceNet"] = "200,00",
                ["Lines[0].VatPercentage"] = "21",
                ["Lines[0].SurchargePercentage"] = "0"
            }));
        Assert.Equal(HttpStatusCode.Redirect, createQuoteResponse.StatusCode);
        var quoteLocation = createQuoteResponse.Headers.Location?.ToString() ?? string.Empty;
        Assert.StartsWith("/facturacion/presupuestos/", quoteLocation, StringComparison.OrdinalIgnoreCase);

        var quoteId = Guid.Parse(quoteLocation.Split('/', StringSplitOptions.RemoveEmptyEntries).Last());

        // 2b. Mark Quote as Aceptado via POST /facturacion/presupuestos/{id}/estado
        uint quoteRowVersion;
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IFacturacionService>();
            var quoteDto = await service.GetQuoteByIdAsync(quoteId);
            Assert.NotNull(quoteDto);
            quoteRowVersion = quoteDto.RowVersion;
        }

        var quoteAcceptToken = await GetAntiforgeryTokenFromPageAsync(ownerClient, quoteLocation);
        var acceptQuoteResponse = await ownerClient.PostAsync(
            $"/facturacion/presupuestos/{quoteId}/estado",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = quoteAcceptToken,
                ["Id"] = quoteId.ToString(),
                ["NewStatus"] = QuoteStatus.Aceptado.ToString(),
                ["RowVersion"] = quoteRowVersion.ToString(CultureInfo.InvariantCulture)
            }));
        Assert.Equal(HttpStatusCode.Redirect, acceptQuoteResponse.StatusCode);

        // 3. Convert Accepted Quote to Draft Invoice via POST /facturacion/presupuestos/{id}/convertir
        var quoteDetailsToken = await GetAntiforgeryTokenFromPageAsync(ownerClient, quoteLocation);
        var convertResponse = await ownerClient.PostAsync(
            $"/facturacion/presupuestos/{quoteId}/convertir",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = quoteDetailsToken
            }));
        Assert.Equal(HttpStatusCode.Redirect, convertResponse.StatusCode);
        var invoiceLocation = convertResponse.Headers.Location?.ToString() ?? string.Empty;
        Assert.StartsWith("/facturacion/facturas/", invoiceLocation, StringComparison.OrdinalIgnoreCase);

        var invoiceId = Guid.Parse(invoiceLocation.Split('/', StringSplitOptions.RemoveEmptyEntries).Last());

        // 4. Issue Definitive Invoice via POST /facturacion/facturas/{id}/emitir
        uint draftRowVersion;
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IFacturacionService>();
            var draft = await service.GetInvoiceByIdAsync(invoiceId);
            Assert.NotNull(draft);
            Assert.Equal(InvoiceStatus.Borrador, draft.Status);
            draftRowVersion = draft.RowVersion;
        }

        var invoiceDetailsToken = await GetAntiforgeryTokenFromPageAsync(ownerClient, invoiceLocation);
        var issueResponse = await ownerClient.PostAsync(
            $"/facturacion/facturas/{invoiceId}/emitir",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = invoiceDetailsToken,
                ["Id"] = invoiceId.ToString(),
                ["RowVersion"] = draftRowVersion.ToString(CultureInfo.InvariantCulture)
            }));
        Assert.Equal(HttpStatusCode.Redirect, issueResponse.StatusCode);

        // 5. Download PDF via GET /facturacion/facturas/{id}/pdf and verify audit log
        var pdfResponse = await ownerClient.GetAsync($"/facturacion/facturas/{invoiceId}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.Equal("application/pdf", pdfResponse.Content.Headers.ContentType?.MediaType);

        var pdfBytes = await pdfResponse.Content.ReadAsByteArrayAsync();
        var pdfHeader = Encoding.ASCII.GetString(pdfBytes, 0, 8);
        Assert.StartsWith("%PDF-1.4", pdfHeader, StringComparison.Ordinal);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();
            var pdfAuditExists = await db.AuditLogs.AnyAsync(
                a => a.Action == "Facturacion.Invoice.DownloadPdf" && a.EntityId == invoiceId.ToString());
            Assert.True(pdfAuditExists);
        }

        // 6. Issue Total Rectifying Invoice via POST /facturacion/facturas/{id}/rectificar
        var rectifyFormPath = $"/facturacion/facturas/{invoiceId}/rectificar";
        var rectifyToken = await GetAntiforgeryTokenFromPageAsync(ownerClient, rectifyFormPath);
        var rectifyResponse = await ownerClient.PostAsync(
            rectifyFormPath,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = rectifyToken,
                ["OriginalInvoiceId"] = invoiceId.ToString(),
                ["Scope"] = RectificationScope.Total.ToString(),
                ["Reason"] = "Anulación completa por error en datos del pedido",
                ["PaymentMethod"] = BillingPaymentMethod.Contado.ToString()
            }));
        Assert.Equal(HttpStatusCode.Redirect, rectifyResponse.StatusCode);

        var rectifyingLocation = rectifyResponse.Headers.Location?.ToString() ?? string.Empty;
        var rectifyingId = Guid.Parse(rectifyingLocation.Split('/', StringSplitOptions.RemoveEmptyEntries).Last());

        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IFacturacionService>();
            var originalAfterRectify = await service.GetInvoiceByIdAsync(invoiceId);
            Assert.NotNull(originalAfterRectify);
            Assert.Equal(InvoiceStatus.Emitida, originalAfterRectify.Status);
            Assert.Equal(200.00m, originalAfterRectify.SubtotalNet);
            Assert.Equal(242.00m, originalAfterRectify.TotalPayable);

            var rectifyingInvoice = await service.GetInvoiceByIdAsync(rectifyingId);
            Assert.NotNull(rectifyingInvoice);
            Assert.Equal(InvoiceDocumentType.Rectificativa, rectifyingInvoice.InvoiceType);
            Assert.Equal(InvoiceStatus.Emitida, rectifyingInvoice.Status);
            Assert.Equal(invoiceId, rectifyingInvoice.RectifiedInvoiceId);
            Assert.Equal(-200.00m, rectifyingInvoice.SubtotalNet);
            Assert.Equal(-242.00m, rectifyingInvoice.TotalPayable);
        }
    }

    [Fact]
    public async Task FacturacionWeb_DemoSandbox_AllowsQuotesAndDrafts_BlocksDefinitiveEmission_And_CleansUpOnLogout()
    {
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

        var loginToken = await GetAntiforgeryTokenFromPageAsync(demoClient, "/account/login");
        var demoLoginResponse = await demoClient.PostAsync("/account/demo-login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = loginToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, demoLoginResponse.StatusCode);

        // 1. Demo user can access /facturacion?tab=presupuestos and sees seeded demo quote
        var factQuotesResponse = await demoClient.GetAsync("/facturacion?tab=presupuestos");
        Assert.Equal(HttpStatusCode.OK, factQuotesResponse.StatusCode);
        var factQuotesHtml = await factQuotesResponse.Content.ReadAsStringAsync();
        Assert.Contains("Cafeter", factQuotesHtml);

        // 2. Demo user can create a draft invoice
        var draftCreateToken = await GetAntiforgeryTokenFromPageAsync(demoClient, "/facturacion/facturas/crear");
        var createDraftResponse = await demoClient.PostAsync(
            "/facturacion/facturas/crear",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = draftCreateToken,
                ["InvoiceType"] = InvoiceDocumentType.Ordinaria.ToString(),
                ["CustomerName"] = "Cliente Demo Borrador S.L.",
                ["CustomerTaxId"] = "B12345674",
                ["CustomerAddress"] = "Calle Demo 1, Madrid",
                ["PaymentMethod"] = BillingPaymentMethod.Contado.ToString(),
                ["Lines[0].Description"] = "Servicio Demo en Borrador",
                ["Lines[0].Quantity"] = "1",
                ["Lines[0].UnitPriceNet"] = "50,00",
                ["Lines[0].VatPercentage"] = "21",
                ["Lines[0].SurchargePercentage"] = "0"
            }));
        Assert.Equal(HttpStatusCode.Redirect, createDraftResponse.StatusCode);
        var draftLocation = createDraftResponse.Headers.Location?.ToString() ?? string.Empty;
        Assert.StartsWith("/facturacion/facturas/", draftLocation, StringComparison.OrdinalIgnoreCase);
        var demoDraftId = Guid.Parse(draftLocation.Split('/', StringSplitOptions.RemoveEmptyEntries).Last());

        // 3. Demo user is blocked from issuing a definitive invoice (lacks Facturacion.Emitir)
        var draftDetailsToken = await GetAntiforgeryTokenFromPageAsync(demoClient, draftLocation);
        var demoIssueResponse = await demoClient.PostAsync(
            $"/facturacion/facturas/{demoDraftId}/emitir",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = draftDetailsToken,
                ["Id"] = demoDraftId.ToString(),
                ["RowVersion"] = "1"
            }));
        Assert.Equal(HttpStatusCode.Redirect, demoIssueResponse.StatusCode);
        Assert.Contains("/account/access-denied", demoIssueResponse.Headers.Location?.ToString() ?? string.Empty);

        // 4. Demo user is forbidden from accessing /facturacion/configuracion (lacks Facturacion.Configurar)
        var demoSettingsResponse = await demoClient.GetAsync("/facturacion/configuracion");
        Assert.Equal(HttpStatusCode.Redirect, demoSettingsResponse.StatusCode);
        Assert.Contains("/account/access-denied", demoSettingsResponse.Headers.Location?.ToString() ?? string.Empty);

        // 5. On logout, all demo quotes and draft invoices are deleted from PostgreSQL
        var logoutToken = await GetAntiforgeryTokenFromPageAsync(demoClient, "/facturacion");
        var logoutResponse = await demoClient.PostAsync("/account/logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = logoutToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);

        using var scope = sandboxFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();
        var remainingDemoInvoices = await db.Set<Invoice>()
            .IgnoreQueryFilters()
            .Where(i => i.CreatedBy != null && i.CreatedBy.StartsWith(DemoSandboxConstants.DemoUserIdPrefix))
            .CountAsync();
        var remainingDemoQuotes = await db.Set<Quote>()
            .IgnoreQueryFilters()
            .Where(q => q.CreatedBy != null && q.CreatedBy.StartsWith(DemoSandboxConstants.DemoUserIdPrefix))
            .CountAsync();

        Assert.Equal(0, remainingDemoInvoices);
        Assert.Equal(0, remainingDemoQuotes);
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
}
