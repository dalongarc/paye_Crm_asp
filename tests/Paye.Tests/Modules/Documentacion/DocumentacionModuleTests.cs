using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Paye.Core.Auditing;
using Paye.Core.Common;
using Paye.Core.Modules.Clientes;
using Paye.Core.Modules.Documentacion;
using Paye.Infrastructure.Data;
using Paye.Infrastructure.Data.Interceptors;
using Paye.Infrastructure.Modules.Documentacion;
using Paye.Tests.Infrastructure;
using Testcontainers.PostgreSql;

namespace Paye.Tests.Modules.Documentacion;

public sealed class DocumentacionPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("paye_test_documentacion")
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

public sealed class DocumentacionModuleTests(DocumentacionPostgresFixture dbFixture)
    : IClassFixture<DocumentacionPostgresFixture>
{
    [Fact]
    public void DefaultVatPercentage_InDocumentoLineaInputDto_Is21Percent()
    {
        var line = new DocumentoLineaInputDto("Desarrollo web e-Commerce", 1m, 2600m);
        Assert.Equal(21m, line.IvaPorcentaje);
        Assert.Equal(0m, line.DescuentoPorcentaje);
    }

    [Fact]
    public void ValidateAndCalculate_MultiVatAndDiscounts_GroupsByVatAndMatchesTotals()
    {
        var lines = new List<DocumentoLineaInputDto>
        {
            new("Desarrollo Shopify", 1m, 2000m, DescuentoPorcentaje: 10m, IvaPorcentaje: 21m), // Bruto 2000, Dto 200, Base 1800, IVA 378
            new("Subida 50 artículos", 2m, 400m, DescuentoPorcentaje: 0m, IvaPorcentaje: 21m),  // Bruto 800, Dto 0, Base 800, IVA 168
            new("Sesión fotografía", 1m, 300m, DescuentoPorcentaje: 5m, IvaPorcentaje: 10m),    // Bruto 300, Dto 15, Base 285, IVA 28.50
            new("Guía impresa formación", 10m, 15m, DescuentoPorcentaje: 0m, IvaPorcentaje: 4m),// Bruto 150, Dto 0, Base 150, IVA 6.00
            new("Suplido dominio oficial", 1m, 50m, DescuentoPorcentaje: 0m, IvaPorcentaje: 0m) // Bruto 50, Dto 0, Base 50, IVA 0.00
        };

        var (result, error) = DocumentoTotalsCalculator.ValidateAndCalculate(lines, requireAtLeastOneLine: true);

        Assert.Null(error);
        Assert.NotNull(result);

        Assert.Equal(3300.00m, result!.SubtotalBruto);
        Assert.Equal(215.00m, result.TotalDescuento);
        Assert.Equal(3085.00m, result.BaseImponible);
        Assert.Equal(580.50m, result.TotalIva);
        Assert.Equal(3665.50m, result.TotalGeneral);

        // Desglose agrupado en 4 tramos: 21%, 10%, 4%, 0%
        Assert.Equal(4, result.DesgloseIva.Count);

        var iva21 = Assert.Single(result.DesgloseIva, d => d.IvaPorcentaje == 21m);
        Assert.Equal(2600.00m, iva21.BaseImponible);
        Assert.Equal(546.00m, iva21.CuotaIva);
        Assert.Equal(3146.00m, iva21.TotalConIva);

        var iva10 = Assert.Single(result.DesgloseIva, d => d.IvaPorcentaje == 10m);
        Assert.Equal(285.00m, iva10.BaseImponible);
        Assert.Equal(28.50m, iva10.CuotaIva);

        var iva4 = Assert.Single(result.DesgloseIva, d => d.IvaPorcentaje == 4m);
        Assert.Equal(150.00m, iva4.BaseImponible);
        Assert.Equal(6.00m, iva4.CuotaIva);

        var iva0 = Assert.Single(result.DesgloseIva, d => d.IvaPorcentaje == 0m);
        Assert.Equal(50.00m, iva0.BaseImponible);
        Assert.Equal(0.00m, iva0.CuotaIva);
    }

    [Fact]
    public void ValidateAndCalculate_MidpointRoundingAwayFromZero_MaintainsExactCentConsistency()
    {
        // Dos líneas de 1.025 € o bases que generan .005 € de IVA:
        // Base 10.02 * 21% = 2.1042 -> 2.10
        // Base 10.03 * 21% = 2.1063 -> 2.11
        // Base 1.25 con descuento 50% = 0.625 -> Descuento 0.63 (AwayFromZero), Base 0.62
        var lines = new List<DocumentoLineaInputDto>
        {
            new("Concepto A", 1m, 1.25m, DescuentoPorcentaje: 50m, IvaPorcentaje: 21m),
            new("Concepto B", 1m, 10.03m, DescuentoPorcentaje: 0m, IvaPorcentaje: 21m)
        };

        var (result, error) = DocumentoTotalsCalculator.ValidateAndCalculate(lines, requireAtLeastOneLine: true);

        Assert.Null(error);
        Assert.NotNull(result);

        var lineA = result!.Lineas[0];
        Assert.Equal(1.25m, lineA.ImporteBruto);
        Assert.Equal(0.63m, lineA.ImporteDescuento); // 1.25 * 0.5 = 0.625 -> 0.63 AwayFromZero
        Assert.Equal(0.62m, lineA.BaseImponible);
        Assert.Equal(0.13m, lineA.CuotaIva); // 0.62 * 0.21 = 0.1302 -> 0.13
        Assert.Equal(0.75m, lineA.TotalLinea);

        // Coherencia exacta entre suma de líneas, suma de desglose y total general
        Assert.Equal(result.Lineas.Sum(l => l.TotalLinea), result.TotalGeneral);
        Assert.Equal(result.DesgloseIva.Sum(d => d.TotalConIva), result.TotalGeneral);
        Assert.Equal(result.BaseImponible + result.TotalIva, result.TotalGeneral);
    }

    [Fact]
    public void ValidateAndCalculate_EmptyLinesAndInvalidVat_ReturnsExpectedValidationResults()
    {
        var (emptyPresupuesto, errPresupuesto) = DocumentoTotalsCalculator.ValidateAndCalculate([], requireAtLeastOneLine: true);
        Assert.Null(emptyPresupuesto);
        Assert.NotNull(errPresupuesto);

        var (emptyContrato, errContrato) = DocumentoTotalsCalculator.ValidateAndCalculate([], requireAtLeastOneLine: false);
        Assert.Null(errContrato);
        Assert.NotNull(emptyContrato);
        Assert.Equal(0m, emptyContrato!.TotalGeneral);

        var invalidVatLines = new List<DocumentoLineaInputDto>
        {
            new("Servicio con IVA inválido", 1m, 100m, 0m, IvaPorcentaje: 18m)
        };
        var (invalidVatResult, errInvalidVat) = DocumentoTotalsCalculator.ValidateAndCalculate(invalidVatLines, requireAtLeastOneLine: true);
        Assert.Null(invalidVatResult);
        Assert.Contains("21%, 10%, 4% y 0%", errInvalidVat);
    }

    [Fact]
    public void HtmlSanitizer_StripsScriptsEventsAndExternalUrls_PreservesSafeMarkup()
    {
        var malicious = """
            <p onclick="alert(1)" class="doc-paragraph">Texto en <strong>negrita</strong> y <em>cursiva</em>.</p>
            <script>window.stealCookies()</script>
            <iframe src="https://evil.example.com"></iframe>
            <img src="https://evil.example.com/track.png" onerror="alert(2)" />
            <img src="/images/documentacion/logo-paye-dark.png" alt="Logo" onclick="evil()" />
            <table><tr><td colspan="2" style="background:url(javascript:alert(1))">Celda</td></tr></table>
            """;

        var sanitized = DocumentoHtmlSanitizer.SanitizeHtml(malicious);

        Assert.DoesNotContain("<script", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evil.example.com", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("style=", sanitized, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("<p class=\"doc-paragraph\">Texto en <strong>negrita</strong> y <em>cursiva</em>.</p>", sanitized);
        Assert.Contains("<img src=\"/images/documentacion/logo-paye-dark.png\" alt=\"Logo\" />", sanitized);
        Assert.Contains("<td colspan=\"2\">Celda</td>", sanitized);

        Assert.True(DocumentoHtmlSanitizer.IsValidLogoOrImageUrl("/images/documentacion/logo-paye-dark.png"));
        Assert.True(DocumentoHtmlSanitizer.IsValidLogoOrImageUrl("data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        Assert.False(DocumentoHtmlSanitizer.IsValidLogoOrImageUrl("https://external.com/logo.png"));
        Assert.False(DocumentoHtmlSanitizer.IsValidLogoOrImageUrl("/images/../../secret.png"));
    }

    [Fact]
    public async Task ConcurrentDocumentCreation_GeneratesSequentialNumbersWithoutDuplicatesOrGaps_AndResetsPerYear()
    {
        TestDatabaseGuard.EnsureIsolatedTestDatabase(dbFixture.ConnectionString);

        var services = new ServiceCollection();
        services.AddScoped<ICurrentUserService>(_ => new FakeCurrentUserService("test-admin-id"));
        services.AddScoped<IAuditLogger, NoOpAuditLogger>();
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();
        services.AddDbContext<PayeDbContext>((sp, options) =>
        {
            options.UseNpgsql(dbFixture.ConnectionString);
            options.AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
        });
        services.AddScoped<IDocumentacionService, DocumentacionService>();

        await using var provider = services.BuildServiceProvider();

        Guid clienteId;
        await using (var setupScope = provider.CreateAsyncScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<PayeDbContext>();
            var cliente = new Cliente
            {
                Id = Guid.NewGuid(),
                NombreFiscal = "Alpe Comercio S.L.",
                NombreComercial = "Alpe",
                CifNif = "B12345678",
                Email = "info@alpe.test",
                Telefono = "600123456",
                Direccion = "Calle Mayor 10",
                CodigoPostal = "15001",
                Ciudad = "A Coruña",
                Provincia = "A Coruña",
                Activo = true
            };
            db.Set<Cliente>().Add(cliente);
            await db.SaveChangesAsync();
            clienteId = cliente.Id;
        }

        // Lanzar 8 creaciones simultáneas en paralelo para el año 2026
        const int concurrentCount = 8;
        var tasks = Enumerable.Range(1, concurrentCount).Select(async i =>
        {
            await using var scope = provider.CreateAsyncScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDocumentacionService>();
            return await svc.CreateAsync(new SaveDocumentoComercialRequest
            {
                Tipo = TipoDocumentoComercial.Presupuesto,
                ClienteId = clienteId,
                Titulo = $"Propuesta Concurrente {i}",
                FechaEmision = new DateOnly(2026, 11, 16),
                Lineas =
                [
                    new DocumentoLineaInputDto($"Servicio {i}", 1m, 1000m, 0m, 21m)
                ]
            });
        });

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r.Succeeded, r.ErrorMessage));

        var assignedNumbers = results
            .Select(r => r.Data!.NumeroDocumento)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var expectedNumbers = Enumerable.Range(1, concurrentCount)
            .Select(n => $"PRES-2026-{n:000}")
            .ToList();

        Assert.Equal(expectedNumbers, assignedNumbers);

        // Verificar reinicio anual automático en 2027 (PRES-2027-001)
        await using (var scope2027 = provider.CreateAsyncScope())
        {
            var svc = scope2027.ServiceProvider.GetRequiredService<IDocumentacionService>();
            var res2027 = await svc.CreateAsync(new SaveDocumentoComercialRequest
            {
                Tipo = TipoDocumentoComercial.Presupuesto,
                ClienteId = clienteId,
                Titulo = "Propuesta Nuevo Ejercicio 2027",
                FechaEmision = new DateOnly(2027, 1, 10),
                Lineas = [new DocumentoLineaInputDto("Servicio 2027", 1m, 500m, 0m, 21m)]
            });

            Assert.True(res2027.Succeeded);
            Assert.Equal("PRES-2027-001", res2027.Data!.NumeroDocumento);
        }
    }

    [Fact]
    public async Task PdfExportService_ShouldBuildOfflineSelfContainedHtml_AndGenerateVectorPdfBytes()
    {
        TestDatabaseGuard.EnsureIsolatedTestDatabase(dbFixture.ConnectionString);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ICurrentUserService>(_ => new FakeCurrentUserService("test-admin-id"));
        services.AddScoped<IAuditLogger, NoOpAuditLogger>();
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();
        services.AddDbContext<PayeDbContext>((sp, options) =>
        {
            options.UseNpgsql(dbFixture.ConnectionString);
            options.AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
        });
        services.AddScoped<IDocumentacionService, DocumentacionService>();
        services.AddScoped<IDocumentoPdfExportService, DocumentoPdfExportService>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();
        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            NombreFiscal = "Alpe Comercio S.L.",
            NombreComercial = "Alpe",
            CifNif = "B98765432",
            Email = "pdf@alpe.test",
            Telefono = "600987654",
            Direccion = "Avenida Marina 5",
            CodigoPostal = "15002",
            Ciudad = "A Coruña",
            Provincia = "A Coruña",
            Activo = true
        };
        db.Set<Cliente>().Add(cliente);
        await db.SaveChangesAsync();

        var docSvc = scope.ServiceProvider.GetRequiredService<IDocumentacionService>();
        var created = await docSvc.CreateAsync(new SaveDocumentoComercialRequest
        {
            Tipo = TipoDocumentoComercial.Presupuesto,
            ClienteId = cliente.Id,
            Titulo = "Web para aumentar venta online",
            Subtitulo = "Diseño Web",
            Categoria = "Diseño Web",
            FechaEmision = new DateOnly(2026, 2, 16),
            FechaValidez = new DateOnly(2026, 3, 18),
            Bloques =
            [
                new DocumentoBloqueDto("blk-1", "1", "pill-section", "1.", "INTRODUCCIÓN", "<p>Propuesta comercial para <strong>Alpe</strong>.</p>", true, 1, false),
                new DocumentoBloqueDto("blk-2", "2", "pricing-table", "2.", "PRESUPUESTO DESGLOSADO", "<p>Tabla económica.</p>", true, 2, false)
            ],
            Lineas =
            [
                new DocumentoLineaInputDto("Desarrollo web e-Commerce", 1m, 2600m, 0m, 21m),
                new DocumentoLineaInputDto("Subida 50 primeros artículos", 1m, 400m, 100m, 21m)
            ]
        });

        Assert.True(created.Succeeded, created.ErrorMessage);
        var docDto = created.Data!;
        var companyDto = await docSvc.GetCompanySettingsAsync();

        var pdfSvc = scope.ServiceProvider.GetRequiredService<IDocumentoPdfExportService>();

        // 1. Verificar el HTML A4 autocontenido y su CSP offline (cero acceso a red externa)
        var selfContainedHtml = pdfSvc.BuildSelfContainedA4Html(
            docDto,
            companyDto,
            new DocumentoA4LayoutOptionsDto());

        Assert.Contains("default-src 'none'; img-src data:; font-src data:; style-src 'unsafe-inline';", selfContainedHtml);
        Assert.Contains("class=\"doc-a4-sheet doc-a4-cover\"", selfContainedHtml);
        Assert.Contains("class=\"doc-a4-sheet doc-a4-inner\"", selfContainedHtml);
        Assert.Contains("@page { size: A4; margin: 0 !important; }", selfContainedHtml);
        Assert.Contains("print-color-adjust: exact !important;", selfContainedHtml);
        Assert.Contains("data:font/woff2;base64,", selfContainedHtml);
        Assert.Contains("data:image/png;base64,", selfContainedHtml);
        Assert.DoesNotContain("http://", selfContainedHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", selfContainedHtml, StringComparison.OrdinalIgnoreCase);

        // Verificar que el HTML del servidor es 100% independiente y NO incluye "Skip to Main" ni menús del shell de la app
        Assert.DoesNotContain("Skip to Main", selfContainedHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Gastos y Trimestre", selfContainedHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sistema de Diseño", selfContainedHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Volver al listado", selfContainedHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Panel general", selfContainedHtml, StringComparison.OrdinalIgnoreCase);

        // 2. Verificar la generación real del binario PDF A4 vectorial en el servidor
        var pdfResult = await pdfSvc.ExportPdfByIdAsync(docDto.Id, new DocumentoA4LayoutOptionsDto());

        Assert.True(pdfResult.Succeeded, pdfResult.ErrorMessage);
        Assert.NotNull(pdfResult.Data);
        Assert.EndsWith(".pdf", pdfResult.Data!.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.True(pdfResult.Data.PdfBytes.Length > 10_000, "El PDF vectorial generado debe tener contenido válido.");

        // Magic bytes de PDF (%PDF-1.)
        var headerAscii = System.Text.Encoding.ASCII.GetString(pdfResult.Data.PdfBytes.AsSpan(0, 7));
        Assert.StartsWith("%PDF-1.", headerAscii, StringComparison.Ordinal);

        // Comprobar que el PDF generado tampoco contiene "Skip to Main" ni textos del menú lateral de la app
        var rawPdfText = System.Text.Encoding.Latin1.GetString(pdfResult.Data.PdfBytes);
        Assert.DoesNotContain("Skip to Main", rawPdfText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Gastos y Trimestre", rawPdfText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sistema de Diseño", rawPdfText, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeCurrentUserService(string userId) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? UserEmail => "admin@paye.test";
        public string? IpAddress => "127.0.0.1";
        public bool IsAuthenticated => true;
        public bool IsDemoSandboxUser => false;
        public string GetActorIdentifier() => userId;
    }

    private sealed class NoOpAuditLogger : IAuditLogger
    {
        public Task LogCriticalActionAsync(
            string action,
            string entityName,
            string? entityId = null,
            string? summary = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
