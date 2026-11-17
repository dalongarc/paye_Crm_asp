using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Paye.Core.Auditing;
using Paye.Core.Common;
using Paye.Core.Integrations;
using Paye.Core.Modules.Documentacion;
using Paye.Infrastructure.Modules.Documentacion;
using Paye.Web.Modules.Documentacion;

namespace Paye.Tests.Modules.Documentacion;

public sealed class DocumentoAiGenerationTests
{
    private sealed class FakeCurrentUserService(string userId) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? UserEmail => $"{userId}@paye.test";
        public string? IpAddress => "127.0.0.1";
        public bool IsAuthenticated => true;
        public bool IsDemoSandboxUser => false;
        public string GetActorIdentifier() => userId;
    }

    private sealed class RecordingAuditLogger : IAuditLogger
    {
        public List<(string Action, string EntityName, string? EntityId, string? Summary)> Entries { get; } = [];

        public Task LogCriticalActionAsync(
            string action,
            string entityName,
            string? entityId = null,
            string? summary = null,
            CancellationToken cancellationToken = default)
        {
            Entries.Add((action, entityName, entityId, summary));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGeminiCompletionProvider(params string[] responses) : IDocumentoGeminiCompletionProvider
    {
        private int _callIndex;
        public int CallCount => _callIndex;

        public Task<(bool Succeeded, string? RawJsonText, string ModelUsed, int HttpStatusCode, bool IsTimeout, string? ErrorMessage)> GenerateStructuredJsonAsync(
            string systemAndUserPrompt,
            string? modelOverride = null,
            CancellationToken cancellationToken = default)
        {
            var idx = Math.Min(_callIndex, responses.Length - 1);
            _callIndex++;
            var raw = responses.Length > 0 ? responses[idx] : "{}";
            return Task.FromResult((true, (string?)raw, "gemini-2.5-flash", 200, false, (string?)null));
        }
    }

    private static IOptions<ExternalIntegrationsSettings> CreateDefaultOptions(int maxChars = 60000, int rateLimit = 10)
    {
        return Options.Create(new ExternalIntegrationsSettings
        {
            Gemini = new GeminiIntegrationOptions
            {
                ApiKey = "configured-in-server",
                Model = "gemini-2.5-flash",
                TimeoutSeconds = 30,
                MaxSourceTextCharacters = maxChars,
                MaxRequestsPerMinutePerUser = rateLimit,
                MaxGeneratedBlocks = 14
            }
        });
    }

    [Fact]
    public async Task GenerarBorradorAsync_InvalidJsonFirstAttempt_RetriesOnceAndSucceedsOnSecondAttempt()
    {
        var validJson = """
        {
          "titulo": "Web Corporativa y Catálogo",
          "subtitulo": "Diseño Web",
          "bloques": [
            {
              "tipo": "texto",
              "titulo": "Introducción",
              "contenido": "<p>Renovamos tu presencia digital para captar clientes.</p>",
              "pendiente": []
            }
          ],
          "lineasSugeridas": [
            { "descripcion": "Diseño y puesta en marcha web", "cantidad": 1 }
          ]
        }
        """;

        var fakeProvider = new FakeGeminiCompletionProvider("NO_ES_JSON_VALIDO", validJson);
        var audit = new RecordingAuditLogger();
        var service = new DocumentoAiGenerationService(
            fakeProvider,
            new DocumentoAiUserRateLimiter(),
            new FakeCurrentUserService("retry-test-user"),
            audit,
            CreateDefaultOptions(),
            NullLogger<DocumentoAiGenerationService>.Instance);

        var result = await service.GenerarBorradorAsync(new GenerarBorradorAiRequest
        {
            Tipo = TipoDocumentoComercial.Presupuesto,
            TextoOrigen = "Manual funcional de tienda online con catálogo de zapatos y contacto directo.",
            EliminarSecretosDetectados = true
        });

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(2, fakeProvider.CallCount);
        Assert.True(result.Data!.ReintentoJsonAplicado);
        Assert.Single(result.Data.Bloques);
        Assert.Single(result.Data.LineasSugeridas);
        Assert.NotEmpty(audit.Entries);
    }

    [Fact]
    public async Task GenerarBorradorAsync_InvalidJsonBothAttempts_FailsCleanlyWithoutTouchingDocument()
    {
        var fakeProvider = new FakeGeminiCompletionProvider("INVALID_1", "INVALID_2");
        var service = new DocumentoAiGenerationService(
            fakeProvider,
            new DocumentoAiUserRateLimiter(),
            new FakeCurrentUserService("invalid-json-user"),
            new RecordingAuditLogger(),
            CreateDefaultOptions(),
            NullLogger<DocumentoAiGenerationService>.Instance);

        var result = await service.GenerarBorradorAsync(new GenerarBorradorAiRequest
        {
            Tipo = TipoDocumentoComercial.Contrato,
            TextoOrigen = "Alcance del proyecto de portal para clientes.",
            EliminarSecretosDetectados = true
        });

        Assert.False(result.Succeeded);
        Assert.Null(result.Data);
        Assert.Equal(2, fakeProvider.CallCount);
        Assert.Contains("esquema", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerarBorradorAsync_ExceedingMaxCharacterLimit_ReturnsValidationError()
    {
        var fakeProvider = new FakeGeminiCompletionProvider("{}");
        var service = new DocumentoAiGenerationService(
            fakeProvider,
            new DocumentoAiUserRateLimiter(),
            new FakeCurrentUserService("size-limit-user"),
            new RecordingAuditLogger(),
            CreateDefaultOptions(maxChars: 500),
            NullLogger<DocumentoAiGenerationService>.Instance);

        var oversizedText = new string('A', 501);
        var result = await service.GenerarBorradorAsync(new GenerarBorradorAiRequest
        {
            Tipo = TipoDocumentoComercial.Presupuesto,
            TextoOrigen = oversizedText
        });

        Assert.False(result.Succeeded);
        Assert.Equal(0, fakeProvider.CallCount);
        Assert.Contains("supera el límite", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerarBorradorAsync_ExceedingPerUserRateLimit_ReturnsRateLimitedResult()
    {
        var validJson = """
        {
          "titulo": "Propuesta",
          "subtitulo": "Web",
          "bloques": [
            { "tipo": "texto", "titulo": "Introducción", "contenido": "<p>Texto claro.</p>", "pendiente": [] }
          ],
          "lineasSugeridas": []
        }
        """;

        var limiter = new DocumentoAiUserRateLimiter();
        var fakeProvider = new FakeGeminiCompletionProvider(validJson);
        var service = new DocumentoAiGenerationService(
            fakeProvider,
            limiter,
            new FakeCurrentUserService("rate-limited-user"),
            new RecordingAuditLogger(),
            CreateDefaultOptions(rateLimit: 2),
            NullLogger<DocumentoAiGenerationService>.Instance);

        var req = new GenerarBorradorAiRequest
        {
            Tipo = TipoDocumentoComercial.Presupuesto,
            TextoOrigen = "Descripción funcional breve del proyecto.",
            EliminarSecretosDetectados = true
        };

        var r1 = await service.GenerarBorradorAsync(req);
        var r2 = await service.GenerarBorradorAsync(req);
        var r3 = await service.GenerarBorradorAsync(req);

        Assert.True(r1.Succeeded);
        Assert.True(r2.Succeeded);
        Assert.False(r3.Succeeded);
        Assert.True(r3.IsRateLimited);
    }

    [Fact]
    public void ValidateAndSanitizeJson_StripsDisallowedHtmlTagsFromAiResponse()
    {
        var rawJson = """
        {
          "titulo": "Propuesta Segura",
          "subtitulo": "Diseño Web",
          "bloques": [
            {
              "tipo": "texto",
              "titulo": "Introducción",
              "contenido": "<p>Texto válido</p><iframe src='https://evil.example'></iframe><script>alert(1)</script>",
              "pendiente": []
            }
          ],
          "lineasSugeridas": []
        }
        """;

        var (dto, error) = DocumentoAiSchemaValidator.ValidateAndSanitizeJson(
            rawJson,
            TipoDocumentoComercial.Presupuesto,
            "gemini-2.5-flash",
            120,
            maxBlocks: 14,
            retried: false);

        Assert.Null(error);
        Assert.NotNull(dto);
        var html = dto!.Bloques[0].Contenido;
        Assert.Contains("<p>Texto válido</p>", html);
        Assert.DoesNotContain("<iframe", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocumentacionAiApiController_EndpointsRequireDocumentacionEditarPolicy()
    {
        var controllerType = typeof(DocumentacionAiApiController);
        var methods = new[] { "ScanSecrets", "ExtractFile", "GenerarBorrador", "RegenerarBloque" };

        foreach (var methodName in methods)
        {
            var method = controllerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(method);
            var authorizeAttr = method!.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authorizeAttr);
            Assert.Equal(DocumentacionPermissions.Editar, authorizeAttr!.Policy);
        }
    }

    [Fact]
    public void BuildSelfContainedA4Html_ShouldNotContainSkipToMainOrAppShellMenu_AndEnforceFullBleedA4()
    {
        var pdfSvc = new DocumentoPdfExportService(null!);
        var sampleDoc = new DocumentoComercialDetailDto(
            Id: Guid.NewGuid(),
            Tipo: TipoDocumentoComercial.Presupuesto,
            FiscalYear: 2026,
            SequentialNumber: 1,
            NumeroDocumento: "PRES-2026-001",
            Titulo: "Web para aumentar venta online",
            Subtitulo: "Diseño Web",
            Categoria: "Diseño Web",
            ClienteId: Guid.NewGuid(),
            ClienteNombre: "Alpe",
            ClienteCifNif: "B12345678",
            ClienteDireccion: "A Coruña",
            ClienteEmail: "info@alpe.es",
            ClienteTelefono: "981000000",
            FechaEmision: new DateOnly(2026, 2, 16),
            FechaValidez: new DateOnly(2026, 3, 18),
            ObjetoContrato: null,
            DuracionContrato: null,
            PrecioTextoContrato: null,
            FormaPagoContrato: null,
            LugarFirma: null,
            Bloques:
            [
                new DocumentoBloqueDto("1", "1", "pill-section", "1.", "INTRODUCCIÓN", "<p>Texto de prueba.</p>", true, 1, false),
                new DocumentoBloqueDto("2", "2", "pricing-table", "2.", "COSTES", "<p>Desglose.</p>", true, 2, false)
            ],
            Lineas:
            [
                new DocumentoLineaDto(Guid.NewGuid(), 1, "Desarrollo web", 1m, 1900m, 0m, 21m, 1900m, 0m, 1900m, 399m, 2299m)
            ],
            DesgloseIva:
            [
                new DesgloseIvaItemDto(21m, "IVA 21%", 1900m, 399m, 2299m)
            ],
            SubtotalBruto: 1900m,
            TotalDescuento: 0m,
            BaseImponible: 1900m,
            TotalIva: 399m,
            TotalGeneral: 2299m,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            RowVersion: 1,
            TextoOrigenDatos: null,
            NombreArchivoOrigen: null);

        var company = new DocumentacionCompanySettingsDto(
            Id: Guid.NewGuid(),
            NombreEmpresa: "Payé Digital",
            CifNif: "B15987456",
            Direccion: "A Coruña",
            Telefono: "+34 600 000 000",
            Email: "hola@payedigital.com",
            Web: "www.payedigital.com",
            Iban: "ES00 0000",
            LogoUrl: "/assets/documentacion/logo-paye-horizontal-dark.png",
            TextoLegalTitulo: "NOTA CONFIDENCIAL",
            TextoLegalContenido: "Aviso legal.",
            RowVersion: 1,
            ModeloIa: "gemini-2.5-flash");

        var html = pdfSvc.BuildSelfContainedA4Html(sampleDoc, company);

        Assert.DoesNotContain("Skip to Main", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"#content\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Gastos y Trimestre", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sistema de Diseño", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Volver al listado", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@page { size: A4; margin: 0 !important; }", html);
        Assert.Contains("print-color-adjust: exact !important;", html);
        Assert.Contains("-webkit-print-color-adjust: exact !important;", html);
        Assert.Contains("width: 210mm !important;", html);
        Assert.Contains("height: 297mm !important;", html);
    }

    [Fact]
    public async Task GenerarBorradorAsync_WithStructuredFallbackProvider_ExtractsOnlyUserSourceMaterialAndNeverLeaksPrompt()
    {
        using var httpClient = new HttpClient();
        var options = Options.Create(new ExternalIntegrationsSettings
        {
            Gemini = new GeminiIntegrationOptions
            {
                ApiKey = string.Empty,
                Model = "gemini-3.8-flash",
                TimeoutSeconds = 10,
                MaxSourceTextCharacters = 60000,
                MaxRequestsPerMinutePerUser = 15,
                MaxGeneratedBlocks = 14
            }
        });

        var realProvider = new DocumentoGeminiCompletionProvider(
            httpClient,
            options,
            NullLogger<DocumentoGeminiCompletionProvider>.Instance);

        var service = new DocumentoAiGenerationService(
            realProvider,
            new DocumentoAiUserRateLimiter(),
            new FakeCurrentUserService("source-isolation-user"),
            new RecordingAuditLogger(),
            options,
            NullLogger<DocumentoAiGenerationService>.Instance);

        var manualRiaMar = """
            # Manual de Documentación Integral — Sistema RíaMar
            Plataforma integral para la gestión comercial, trazabilidad de pedidos y atención a clientes del sector marítimo y pesquero.

            ## 1. Gestión de Pedidos y Clientes
            - Registro rápido de pedidos desde móvil o mostrador con confirmación inmediata.
            - Historial completo de compras y preferencias de cada cliente.
            - Envío automático de albaranes y presupuestos en PDF con un clic.

            ## 2. Control de Lotes y Trazabilidad
            - Seguimiento de capturas, lotes y fechas de entrada para cumplir la normativa sanitaria.
            - Etiquetado claro y consulta rápida desde cualquier dispositivo.

            ## 3. Despliegue Técnico y Base de Datos
            - Contenedor docker-compose con PostgreSQL 16 en localhost:5432.
            """;

        var result = await service.GenerarBorradorAsync(new GenerarBorradorAiRequest
        {
            Tipo = TipoDocumentoComercial.Presupuesto,
            TextoOrigen = manualRiaMar,
            EliminarSecretosDetectados = true
        });

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal("Sistema RíaMar", result.Data!.Titulo);
        Assert.NotEmpty(result.Data.Bloques);
        Assert.NotEmpty(result.Data.LineasSugeridas);

        foreach (var bloque in result.Data.Bloques)
        {
            Assert.DoesNotContain("Eres un redactor comercial", bloque.Contenido, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("MATERIAL_ORIGEN_DATOS", bloque.Contenido, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DATOS PASIVOS DE LECTURA", bloque.Contenido, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Idioma: Español de España", bloque.Contenido, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("docker-compose", bloque.Contenido, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var linea in result.Data.LineasSugeridas)
        {
            Assert.DoesNotContain("MATERIAL_ORIGEN_DATOS", linea.Descripcion, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Idioma:", linea.Descripcion, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Tu salida NUNCA", linea.Descripcion, StringComparison.OrdinalIgnoreCase);
        }
    }
}
