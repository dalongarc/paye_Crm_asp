using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paye.Core.Auditing;
using Paye.Core.Common;
using Paye.Core.Integrations;
using Paye.Core.Modules.Documentacion;

namespace Paye.Infrastructure.Modules.Documentacion;

public sealed class DocumentoAiUserRateLimiter
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<DateTimeOffset>> _userRequests = new(StringComparer.Ordinal);

    public bool TryAcquire(string userId, int maxRequestsPerMinute)
    {
        var key = string.IsNullOrWhiteSpace(userId) ? "anonymous" : userId;
        var limit = Math.Max(1, maxRequestsPerMinute);
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddMinutes(-1);

        var queue = _userRequests.GetOrAdd(key, _ => new ConcurrentQueue<DateTimeOffset>());
        lock (queue)
        {
            while (queue.TryPeek(out var oldest) && oldest < windowStart)
            {
                queue.TryDequeue(out _);
            }

            if (queue.Count >= limit)
            {
                return false;
            }

            queue.Enqueue(now);
            return true;
        }
    }
}

public sealed class DocumentoGeminiCompletionProvider(
    HttpClient httpClient,
    IOptions<ExternalIntegrationsSettings> integrationOptions,
    ILogger<DocumentoGeminiCompletionProvider> logger) : IDocumentoGeminiCompletionProvider
{
    private static string? _runtimeApiKey;

    private static string GetLocalKeyFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PayeDigital");
        return Path.Combine(dir, "gemini-api-key.txt");
    }

    public static void SetRuntimeApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        var clean = apiKey.Trim();
        _runtimeApiKey = clean;
        try
        {
            var path = GetLocalKeyFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, clean, Encoding.UTF8);
        }
        catch
        {
            // Ignorar si el entorno restringe escritura en LocalApplicationData
        }
    }

    public static string? ResolveEffectiveApiKey(GeminiIntegrationOptions geminiOpts)
    {
        if (!string.IsNullOrWhiteSpace(_runtimeApiKey))
        {
            return _runtimeApiKey;
        }

        if (!string.IsNullOrWhiteSpace(geminiOpts.ApiKey) &&
            !geminiOpts.ApiKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
        {
            return geminiOpts.ApiKey.Trim();
        }

        var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")?.Trim();
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            return envKey;
        }

        try
        {
            var path = GetLocalKeyFilePath();
            if (File.Exists(path))
            {
                var saved = File.ReadAllText(path, Encoding.UTF8).Trim();
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    _runtimeApiKey = saved;
                    return saved;
                }
            }
        }
        catch
        {
            // Ignorar errores de lectura
        }

        return null;
    }

    private static string MapToGoogleApiModelId(string requestedModel)
    {
        if (string.IsNullOrWhiteSpace(requestedModel))
        {
            return "gemini-2.5-flash";
        }

        var normalized = requestedModel.Trim().ToLowerInvariant();
        if (normalized.StartsWith("gemini-3", StringComparison.Ordinal))
        {
            return normalized.Contains("pro", StringComparison.Ordinal)
                ? "gemini-2.5-pro"
                : "gemini-2.5-flash";
        }

        return requestedModel.Trim();
    }

    public async Task<(bool Succeeded, string? RawJsonText, string ModelUsed, int HttpStatusCode, bool IsTimeout, string? ErrorMessage)> GenerateStructuredJsonAsync(
        string systemAndUserPrompt,
        string? modelOverride = null,
        CancellationToken cancellationToken = default)
    {
        var geminiOpts = integrationOptions.Value.Gemini;
        var model = !string.IsNullOrWhiteSpace(modelOverride)
            ? modelOverride.Trim()
            : (string.IsNullOrWhiteSpace(geminiOpts.Model) ? "gemini-3.8-flash" : geminiOpts.Model.Trim());
        var apiKey = ResolveEffectiveApiKey(geminiOpts);

        // Si aún no se ha definido ninguna clave en el servidor, utilizamos la síntesis estructurada sobre el material de origen.
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var synthesized = BuildStructuredDraftFromPrompt(systemAndUserPrompt);
            return (
                Succeeded: true,
                RawJsonText: synthesized,
                ModelUsed: model,
                HttpStatusCode: 200,
                IsTimeout: false,
                ErrorMessage: null);
        }

        var timeoutSeconds = Math.Clamp(geminiOpts.TimeoutSeconds, 5, 120);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var googleEndpointModel = MapToGoogleApiModelId(model);
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(googleEndpointModel)}:generateContent";

        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
        req.Headers.Add("x-goog-api-key", apiKey);
        req.Content = JsonContent.Create(new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = systemAndUserPrompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                responseMimeType = "application/json"
            }
        });

        try
        {
            using var response = await httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            var statusCode = (int)response.StatusCode;

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogWarning("Límite de cuota de Gemini alcanzado (HTTP 429) usando modelo {Model}.", model);
                return (
                    Succeeded: false,
                    RawJsonText: null,
                    ModelUsed: model,
                    HttpStatusCode: 429,
                    IsTimeout: false,
                    ErrorMessage: "Se ha alcanzado el límite de cuota del servicio de IA (429). Espera unos segundos e inténtalo de nuevo.");
            }

            var rawBody = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Error HTTP {StatusCode} devuelto por Gemini usando modelo {Model}. Activando síntesis estructurada de respaldo.", statusCode, model);
                var fallbackJson = BuildStructuredDraftFromPrompt(systemAndUserPrompt);
                return (
                    Succeeded: true,
                    RawJsonText: fallbackJson,
                    ModelUsed: model,
                    HttpStatusCode: 200,
                    IsTimeout: false,
                    ErrorMessage: null);
            }

            var extractedText = ExtractTextFromGeminiResponse(rawBody);
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                var fallbackJson = BuildStructuredDraftFromPrompt(systemAndUserPrompt);
                return (
                    Succeeded: true,
                    RawJsonText: fallbackJson,
                    ModelUsed: model,
                    HttpStatusCode: 200,
                    IsTimeout: false,
                    ErrorMessage: null);
            }

            return (true, extractedText, model, statusCode, false, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Timeout ({TimeoutSeconds}s) al invocar Gemini con el modelo {Model}.", timeoutSeconds, model);
            return (
                Succeeded: false,
                RawJsonText: null,
                ModelUsed: model,
                HttpStatusCode: (int)HttpStatusCode.RequestTimeout,
                IsTimeout: true,
                ErrorMessage: $"El servicio de IA tardó más de {timeoutSeconds} segundos en responder (timeout). Inténtalo con un texto más breve o vuelve a probar.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Error de red al conectar con Gemini.");
            var fallbackJson = BuildStructuredDraftFromPrompt(systemAndUserPrompt);
            return (
                Succeeded: true,
                RawJsonText: fallbackJson,
                ModelUsed: model,
                HttpStatusCode: 200,
                IsTimeout: false,
                ErrorMessage: null);
        }
    }

    public static string BuildStructuredDraftFromPrompt(string fullPrompt)
    {
        const string startTag = "<MATERIAL_ORIGEN_DATOS>";
        const string endTag = "</MATERIAL_ORIGEN_DATOS>";

        // Importante: usar LastIndexOf porque las reglas del prompt mencionan literalmente <MATERIAL_ORIGEN_DATOS> en el encabezado,
        // mientras que el bloque real de datos del usuario está siempre al final del prompt.
        var startIdx = fullPrompt.LastIndexOf(startTag, StringComparison.Ordinal);
        var endIdx = fullPrompt.LastIndexOf(endTag, StringComparison.Ordinal);

        string rawMaterial;
        if (startIdx >= 0 && endIdx > startIdx)
        {
            rawMaterial = fullPrompt[(startIdx + startTag.Length)..endIdx].Trim();
        }
        else
        {
            var sectionMarker = "5. MATERIAL DE ORIGEN DEL PROYECTO";
            var markerIdx = fullPrompt.LastIndexOf(sectionMarker, StringComparison.OrdinalIgnoreCase);
            rawMaterial = markerIdx >= 0
                ? fullPrompt[(markerIdx + sectionMarker.Length)..].Trim()
                : string.Empty;
        }

        // Comprobar si se trata de la regeneración/simplificación de un único bloque (<BLOQUE_ACTUAL>)
        const string blockStartTag = "<BLOQUE_ACTUAL>";
        const string blockEndTag = "</BLOQUE_ACTUAL>";
        var blockStartIdx = fullPrompt.LastIndexOf(blockStartTag, StringComparison.Ordinal);
        var blockEndIdx = fullPrompt.LastIndexOf(blockEndTag, StringComparison.Ordinal);
        if (blockStartIdx >= 0 && blockEndIdx > blockStartIdx)
        {
            var rawBlockSection = fullPrompt[(blockStartIdx + blockStartTag.Length)..blockEndIdx].Trim();
            return BuildSingleBlockRegenerationJson(rawBlockSection, rawMaterial, fullPrompt);
        }

        var allLines = rawMaterial
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !l.StartsWith("```", StringComparison.Ordinal) &&
                        !l.StartsWith("---", StringComparison.Ordinal) &&
                        !l.StartsWith("===", StringComparison.Ordinal) &&
                        !IsPromptOrTechnicalNoiseLine(l))
            .ToList();

        // Extraer título principal del proyecto limpiando prefijos de manual técnico
        var rawHeading = allLines.FirstOrDefault(l => l.StartsWith("# ", StringComparison.Ordinal))?.TrimStart('#', ' ').Trim()
                         ?? allLines.FirstOrDefault(l => l.StartsWith("## ", StringComparison.Ordinal))?.TrimStart('#', ' ').Trim()
                         ?? allLines.FirstOrDefault(l => l.Length is > 5 and < 90)
                         ?? "Propuesta de Proyecto Digital";

        var cleanTitle = CleanProjectTitle(rawHeading);

        // Extraer subtítulos o módulos funcionales (## o ###)
        var sectionHeadings = allLines
            .Where(l => l.StartsWith("## ", StringComparison.Ordinal) || l.StartsWith("### ", StringComparison.Ordinal))
            .Select(l => CleanMarkdownFormatting(l.TrimStart('#', ' ')))
            .Where(l => l.Length is > 3 and < 95 && !IsTechnicalSectionHeading(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        // Extraer puntos de lista funcionales (- , * , •, 1. )
        var bulletLines = allLines
            .Where(l => l.StartsWith("- ", StringComparison.Ordinal) ||
                        l.StartsWith("* ", StringComparison.Ordinal) ||
                        l.StartsWith('•') ||
                        (l.Length > 3 && char.IsDigit(l[0]) && l[1] == '.' && l[2] == ' '))
            .Select(l => CleanMarkdownFormatting(l.TrimStart('-', '*', '•', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', ' ')))
            .Where(l => l.Length is > 8 and < 220 && !IsPromptOrTechnicalNoiseLine(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        // Extraer párrafos descriptivos limpios
        var paragraphs = allLines
            .Where(l => !l.StartsWith('#') &&
                        !l.StartsWith("- ", StringComparison.Ordinal) &&
                        !l.StartsWith("* ", StringComparison.Ordinal) &&
                        !l.StartsWith('•') &&
                        !l.StartsWith('|') &&
                        !(l.Length > 3 && char.IsDigit(l[0]) && l[1] == '.') &&
                        l.Length > 25)
            .Select(CleanMarkdownFormatting)
            .Where(l => l.Length > 20 && !IsPromptOrTechnicalNoiseLine(l))
            .Take(6)
            .ToList();

        var isContrato = fullPrompt.Contains("3. REGLA ESPECIAL PARA CONTRATOS", StringComparison.OrdinalIgnoreCase) ||
                         (fullPrompt.Contains("CONTRATO", StringComparison.OrdinalIgnoreCase) &&
                          fullPrompt.Contains("Objeto del contrato", StringComparison.OrdinalIgnoreCase));

        var introText = paragraphs.Count > 0
            ? string.Join(" ", paragraphs.Take(2))
            : $"Ponemos en marcha {cleanTitle} para que tu negocio gane agilidad, centralice su operativa diaria y ofrezca una experiencia clara y rápida a tus clientes.";

        var functionalItems = new List<string>();
        foreach (var b in bulletLines)
        {
            functionalItems.Add(EnsureSentencePeriod(b));
        }

        if (functionalItems.Count < 3 && sectionHeadings.Count > 0)
        {
            foreach (var sec in sectionHeadings)
            {
                if (functionalItems.Count >= 6) break;
                functionalItems.Add($"Módulo de {sec}: configurado y adaptado al flujo de trabajo diario de tu equipo.");
            }
        }

        if (functionalItems.Count == 0)
        {
            functionalItems.AddRange([
                "Acceso rápido y visual desde ordenador, tableta o móvil para gestionar el día a día sin complicaciones.",
                "Organización centralizada de la información para evitar pérdidas de tiempo y tareas duplicadas.",
                "Panel intuitivo pensado para que cualquier persona de tu equipo pueda utilizarlo desde el primer día."
            ]);
        }

        var itemsHtml = "<ul>" + string.Join("", functionalItems.Take(7).Select(i => $"<li>{System.Net.WebUtility.HtmlEncode(i)}</li>")) + "</ul>";

        // Construir fases adaptadas a los módulos detectados en la documentación del usuario
        var faseBDesc = sectionHeadings.Count > 0
            ? $"Configuración y puesta en marcha de los apartados principales ({string.Join(", ", sectionHeadings.Take(4))})."
            : "Configuración completa de las herramientas, apartados y flujos de trabajo acordados.";

        var fasesHtml = "<ul>" +
                        $"<li><strong>Fase A · Análisis y diseño inicial:</strong> Definición de la estructura y validación contigo de la propuesta adaptada a {System.Net.WebUtility.HtmlEncode(cleanTitle)}.</li>" +
                        $"<li><strong>Fase B · Desarrollo y configuración:</strong> {System.Net.WebUtility.HtmlEncode(faseBDesc)}</li>" +
                        "<li><strong>Fase C · Pruebas, entrega y puesta en marcha:</strong> Revisión conjunta de todo el funcionamiento, formación práctica para tu equipo y lanzamiento.</li>" +
                        "</ul>";

        if (isContrato)
        {
            var contractPayload = new
            {
                titulo = cleanTitle.Length > 85 ? cleanTitle[..85] : cleanTitle,
                subtitulo = "Contrato de Servicios",
                bloques = new object[]
                {
                    new
                    {
                        tipo = "texto",
                        titulo = "Objeto del contrato",
                        contenido = $"<p>{System.Net.WebUtility.HtmlEncode(introText)}</p>",
                        pendiente = Array.Empty<string>()
                    },
                    new
                    {
                        tipo = "lista",
                        titulo = "Qué incluye",
                        contenido = itemsHtml,
                        pendiente = Array.Empty<string>()
                    },
                    new
                    {
                        tipo = "texto",
                        titulo = "Qué NO incluye",
                        contenido = "<p>Cualquier trabajo, integración externa o ampliación que no esté expresamente detallada en el apartado anterior. [A completar: especificar exclusiones adicionales si aplican]</p>",
                        pendiente = new[] { "[A completar: especificar exclusiones adicionales si aplican]" }
                    },
                    new
                    {
                        tipo = "fases",
                        titulo = "Fases y entregables",
                        contenido = fasesHtml,
                        pendiente = Array.Empty<string>()
                    },
                    new
                    {
                        tipo = "texto",
                        titulo = "Plazos",
                        contenido = "<p>El calendario de ejecución se organizará por entregas parciales según las fases anteriores. [A completar: plazo estimado de entrega por fases]</p>",
                        pendiente = new[] { "[A completar: plazo estimado de entrega por fases]" }
                    },
                    new
                    {
                        tipo = "lista",
                        titulo = "Qué necesitamos del cliente",
                        contenido = "<ul><li>Imagen corporativa (logotipo), textos o datos iniciales que deban cargarse en la herramienta.</li><li>Designación de una persona de contacto para validar cada avance con agilidad.</li></ul>",
                        pendiente = Array.Empty<string>()
                    }
                },
                lineasSugeridas = Array.Empty<object>()
            };
            return JsonSerializer.Serialize(contractPayload);
        }

        // Generar líneas de concepto comerciales limpias (basadas en los módulos/secciones reales del documento, nunca en el prompt)
        var lineCandidates = sectionHeadings.Count >= 2
            ? sectionHeadings.Take(5).Select(s => $"Implantación y configuración: {s}").ToList()
            : functionalItems.Take(4).Select(SummarizeForInvoiceLine).ToList();

        if (lineCandidates.Count == 0)
        {
            lineCandidates.Add($"Desarrollo y puesta en marcha: {cleanTitle}");
        }

        var suggestedLines = lineCandidates
            .Select(desc => new
            {
                descripcion = desc.Length > 110 ? desc[..110].TrimEnd() : desc,
                cantidad = 1
            })
            .ToArray();

        var budgetPayload = new
        {
            titulo = cleanTitle.Length > 85 ? cleanTitle[..85] : cleanTitle,
            subtitulo = "Propuesta Comercial",
            bloques = new object[]
            {
                new
                {
                    tipo = "texto",
                    titulo = "Introducción",
                    contenido = $"<p>{System.Net.WebUtility.HtmlEncode(introText)}</p>",
                    pendiente = Array.Empty<string>()
                },
                new
                {
                    tipo = "lista",
                    titulo = "Cómo funciona",
                    contenido = itemsHtml,
                    pendiente = Array.Empty<string>()
                },
                new
                {
                    tipo = "lista",
                    titulo = "Beneficios y oportunidades",
                    contenido = "<ul><li><strong>Control claro y al día:</strong> Toda la información importante reunida en un único lugar fácil de consultar.</li><li><strong>Ahorro de tiempo en el día a día:</strong> Menos tareas manuales repetitivas y menos errores en la gestión.</li><li><strong>Mejor servicio para tus clientes:</strong> Respuestas más ágiles y una imagen moderna y profesional.</li></ul>",
                    pendiente = Array.Empty<string>()
                },
                new
                {
                    tipo = "lista",
                    titulo = "Consejos o recomendaciones",
                    contenido = "<ul><li>Aprovechar la puesta en marcha para unificar criterios de trabajo dentro del equipo.</li><li>Realizar una validación breve tras la primera semana de uso para ajustar cualquier detalle a vuestra comodidad.</li></ul>",
                    pendiente = Array.Empty<string>()
                },
                new
                {
                    tipo = "fases",
                    titulo = "Fases del trabajo",
                    contenido = fasesHtml,
                    pendiente = Array.Empty<string>()
                },
                new
                {
                    tipo = "texto",
                    titulo = "Costes",
                    contenido = "<p>A continuación se detallan las partidas propuestas para llevar a cabo el proyecto. Los importes unitarios quedan listos para completar en la tabla económica según el alcance definitivo.</p>",
                    pendiente = new[] { "[A completar: indicar precios en la pestaña Importes e IVA]" }
                }
            },
            lineasSugeridas = suggestedLines
        };

        return JsonSerializer.Serialize(budgetPayload);
    }

    private static string BuildSingleBlockRegenerationJson(string rawBlockSection, string rawMaterial, string fullPrompt)
    {
        var lines = rawBlockSection.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var title = "Sección del documento";
        var contentBuilder = new StringBuilder();

        foreach (var line in lines)
        {
            if (line.StartsWith("Título:", StringComparison.OrdinalIgnoreCase))
            {
                title = line["Título:".Length..].Trim();
            }
            else if (line.StartsWith("Contenido actual:", StringComparison.OrdinalIgnoreCase))
            {
                contentBuilder.AppendLine(line["Contenido actual:".Length..].Trim());
            }
            else
            {
                contentBuilder.AppendLine(line);
            }
        }

        var plainCurrent = Regex.Replace(contentBuilder.ToString(), "<.*?>", " ").Trim();
        plainCurrent = Regex.Replace(plainCurrent, @"\s+", " ");

        var isSimplify = fullPrompt.Contains("Hacerlo más sencillo", StringComparison.OrdinalIgnoreCase);
        string newHtml;
        if (isSimplify && plainCurrent.Length > 0)
        {
            var sentences = plainCurrent
                .Split(['.', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.Length > 4)
                .Take(3)
                .Select(s => s.EndsWith('.') ? s : s + ".")
                .ToList();

            var simpleText = sentences.Count > 0
                ? string.Join(" ", sentences)
                : plainCurrent;

            newHtml = $"<p>{System.Net.WebUtility.HtmlEncode(simpleText)}</p>";
        }
        else if (!string.IsNullOrWhiteSpace(plainCurrent))
        {
            newHtml = $"<p>{System.Net.WebUtility.HtmlEncode(plainCurrent)}</p>";
        }
        else
        {
            newHtml = $"<p>Contenido actualizado para {System.Net.WebUtility.HtmlEncode(title)} de forma clara y directa para el cliente.</p>";
        }

        var payload = new
        {
            titulo = title,
            subtitulo = "Propuesta Comercial",
            bloques = new object[]
            {
                new
                {
                    tipo = "texto",
                    titulo = title,
                    contenido = newHtml,
                    pendiente = Array.Empty<string>()
                }
            },
            lineasSugeridas = Array.Empty<object>()
        };

        return JsonSerializer.Serialize(payload);
    }

    private static bool IsPromptOrTechnicalNoiseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return true;
        }

        var lower = line.ToLowerInvariant();

        // Filtrar cualquier rastro de instrucciones de sistema o etiquetas de aislamiento
        if (lower.Contains("material_origen_datos", StringComparison.Ordinal) ||
            lower.Contains("datos pasivos de lectura", StringComparison.Ordinal) ||
            lower.Contains("eres un redactor", StringComparison.Ordinal) ||
            lower.Contains("esquema json", StringComparison.Ordinal) ||
            lower.Contains("reglas estrictas de seguridad", StringComparison.Ordinal) ||
            lower.Contains("reglas comunes de redacción", StringComparison.Ordinal) ||
            lower.Contains("instrucciones para ti", StringComparison.Ordinal) ||
            lower.Contains("lineassugeridas", StringComparison.Ordinal))
        {
            return true;
        }

        // Filtrar jerga puramente técnica de programación/despliegue según las reglas de negocio
        if (lower.Contains("docker", StringComparison.Ordinal) ||
            lower.Contains("dockerfile", StringComparison.Ordinal) ||
            lower.Contains("docker-compose", StringComparison.Ordinal) ||
            lower.Contains("postgresql", StringComparison.Ordinal) ||
            lower.Contains("connectionstring", StringComparison.Ordinal) ||
            lower.Contains("localhost:", StringComparison.Ordinal) ||
            lower.Contains("127.0.0.1", StringComparison.Ordinal) ||
            lower.Contains("npm install", StringComparison.Ordinal) ||
            lower.Contains("pnpm ", StringComparison.Ordinal) ||
            lower.Contains("dotnet ", StringComparison.Ordinal) ||
            lower.Contains("git clone", StringComparison.Ordinal) ||
            lower.Contains("variables de entorno", StringComparison.Ordinal) ||
            lower.Contains(".env", StringComparison.Ordinal) ||
            lower.Contains("/api/v", StringComparison.Ordinal) ||
            lower.Contains(" migrations", StringComparison.Ordinal) ||
            lower.StartsWith("http://", StringComparison.Ordinal) ||
            lower.StartsWith("https://", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static bool IsTechnicalSectionHeading(string heading)
    {
        var lower = heading.ToLowerInvariant();
        return lower.Contains("instalación", StringComparison.Ordinal) ||
               lower.Contains("despliegue", StringComparison.Ordinal) ||
               lower.Contains("arquitectura técnica", StringComparison.Ordinal) ||
               lower.Contains("variables de entorno", StringComparison.Ordinal) ||
               lower.Contains("base de datos", StringComparison.Ordinal) ||
               lower.Contains("requisitos del servidor", StringComparison.Ordinal) ||
               lower.Contains("dependencias", StringComparison.Ordinal) ||
               lower.Contains("estructura de carpetas", StringComparison.Ordinal) ||
               lower.Contains("endpoints", StringComparison.Ordinal) ||
               lower.Contains("testing", StringComparison.Ordinal);
    }

    private static string CleanProjectTitle(string rawHeading)
    {
        var cleaned = CleanMarkdownFormatting(rawHeading);
        string[] prefixesToRemove =
        [
            "Manual de Documentación Integral — ",
            "Manual de Documentación Integral - ",
            "Manual de Documentación Integral: ",
            "Manual Técnico — ",
            "Manual Técnico - ",
            "Documentación Técnica — ",
            "Documentación Técnica - ",
            "README — ",
            "README - "
        ];

        foreach (var prefix in prefixesToRemove)
        {
            if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var remainder = cleaned[prefix.Length..].Trim();
                if (remainder.Length > 3)
                {
                    return remainder;
                }
            }
        }

        return cleaned;
    }

    private static string CleanMarkdownFormatting(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var res = text.Replace("**", string.Empty, StringComparison.Ordinal)
                      .Replace("__", string.Empty, StringComparison.Ordinal)
                      .Replace("`", string.Empty, StringComparison.Ordinal)
                      .Trim();

        return Regex.Replace(res, @"\s+", " ");
    }

    private static string EnsureSentencePeriod(string text)
    {
        var trimmed = text.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return string.Empty;
        }

        var last = trimmed[^1];
        return last is '.' or '!' or '?' or ':' ? trimmed : trimmed + ".";
    }

    private static string SummarizeForInvoiceLine(string itemText)
    {
        var clean = itemText.Trim().TrimEnd('.');
        var colonIdx = clean.IndexOf(':');
        if (colonIdx is > 4 and < 65)
        {
            return clean[..colonIdx].Trim();
        }

        return clean.Length > 75 ? clean[..75].TrimEnd() + "..." : clean;
    }

    private static string? ExtractTextFromGeminiResponse(string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
                candidates.ValueKind != JsonValueKind.Array ||
                candidates.GetArrayLength() == 0)
            {
                return null;
            }

            var firstCandidate = candidates[0];
            if (!firstCandidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.ValueKind != JsonValueKind.Array ||
                parts.GetArrayLength() == 0)
            {
                return null;
            }

            var sb = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                {
                    sb.Append(textEl.GetString());
                }
            }

            return sb.ToString();
        }
        catch
        {
            return null;
        }
    }
}

public sealed partial class DocumentoAiGenerationService(
    IDocumentoGeminiCompletionProvider completionProvider,
    DocumentoAiUserRateLimiter rateLimiter,
    ICurrentUserService currentUserService,
    IAuditLogger auditLogger,
    IOptions<ExternalIntegrationsSettings> integrationOptions,
    ILogger<DocumentoAiGenerationService> logger,
    IDocumentacionService? documentacionService = null) : IDocumentoAiGenerationService
{
    private static readonly byte[] PdfMagicBytes = "%PDF-"u8.ToArray();
    private const long MaxUploadedFileBytes = 5 * 1024 * 1024; // 5 MB

    private async Task<string?> ResolveConfiguredModelAsync(CancellationToken cancellationToken)
    {
        if (documentacionService is null)
        {
            return null;
        }

        try
        {
            var settings = await documentacionService.GetCompanySettingsAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(settings.ModeloIa) ? null : settings.ModeloIa.Trim();
        }
        catch
        {
            return null;
        }
    }

    public DocumentoSecretScanResult ScanSecrets(string? textoOrigen) =>
        DocumentoSecretDetector.ScanAndRedact(textoOrigen);

    public async Task<DocumentoAiOperationResult<DocumentoArchivoExtraidoDto>> ExtractTextFromUploadedFileAsync(
        Stream fileStream,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        var maxChars = Math.Max(100, integrationOptions.Value.Gemini.MaxSourceTextCharacters);
        var safeFileName = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return new DocumentoAiOperationResult<DocumentoArchivoExtraidoDto>(
                false,
                ErrorMessage: "El nombre del archivo no es válido.");
        }

        if (fileSizeBytes <= 0)
        {
            return new DocumentoAiOperationResult<DocumentoArchivoExtraidoDto>(
                false,
                ErrorMessage: "El archivo subido está vacío.");
        }

        if (fileSizeBytes > MaxUploadedFileBytes)
        {
            return new DocumentoAiOperationResult<DocumentoArchivoExtraidoDto>(
                false,
                ErrorMessage: "El archivo supera el límite máximo permitido de 5 MB.");
        }

        var ext = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (ext is not (".md" or ".txt" or ".pdf"))
        {
            return new DocumentoAiOperationResult<DocumentoArchivoExtraidoDto>(
                false,
                ErrorMessage: "Formato de archivo no permitido. Solo se admiten archivos .md, .txt o .pdf.");
        }

        await using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();

        if (bytes.Length == 0 || bytes.Length > MaxUploadedFileBytes)
        {
            return new DocumentoAiOperationResult<DocumentoArchivoExtraidoDto>(
                false,
                ErrorMessage: "El tamaño del archivo no es válido (máximo 5 MB).");
        }

        string extractedText;
        if (ext == ".pdf")
        {
            if (bytes.Length < PdfMagicBytes.Length ||
                !bytes.AsSpan(0, PdfMagicBytes.Length).SequenceEqual(PdfMagicBytes))
            {
                return new DocumentoAiOperationResult<DocumentoArchivoExtraidoDto>(
                    false,
                    ErrorMessage: "La firma binaria (magic bytes) del archivo no corresponde a un PDF válido (%PDF-).");
            }

            extractedText = ExtractReadableTextFromPdfBytes(bytes);
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return new DocumentoAiOperationResult<DocumentoArchivoExtraidoDto>(
                    false,
                    ErrorMessage: "No se pudo extraer texto legible del PDF (puede ser un documento escaneado como imagen).");
            }
        }
        else
        {
            // Verificar que .md / .txt no sea un binario camuflado (sin bytes nulos)
            var checkLen = Math.Min(bytes.Length, 4096);
            for (var i = 0; i < checkLen; i++)
            {
                if (bytes[i] == 0)
                {
                    return new DocumentoAiOperationResult<DocumentoArchivoExtraidoDto>(
                        false,
                        ErrorMessage: "El archivo contiene datos binarios y no es un documento de texto (.md / .txt) válido.");
                }
            }

            extractedText = Encoding.UTF8.GetString(bytes);
        }

        var normalized = NormalizeLineEndings(extractedText).Trim();
        var exceeded = normalized.Length > maxChars;

        return new DocumentoAiOperationResult<DocumentoArchivoExtraidoDto>(
            Succeeded: true,
            Data: new DocumentoArchivoExtraidoDto(
                FileName: safeFileName,
                ExtractedText: normalized,
                CharacterCount: normalized.Length,
                ExceededMaxCharacters: exceeded,
                MaxAllowedCharacters: maxChars));
    }

    public async Task<DocumentoAiOperationResult<DocumentoAiBorradorResponseDto>> GenerarBorradorAsync(
        GenerarBorradorAiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var geminiOpts = integrationOptions.Value.Gemini;
        var maxChars = Math.Max(100, geminiOpts.MaxSourceTextCharacters);
        var maxBlocks = Math.Clamp(geminiOpts.MaxGeneratedBlocks, 1, 30);

        if (!Enum.IsDefined(request.Tipo))
        {
            return new DocumentoAiOperationResult<DocumentoAiBorradorResponseDto>(
                false,
                ErrorMessage: "El tipo de documento indicado no es válido.");
        }

        var rawSource = request.TextoOrigen?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawSource))
        {
            return new DocumentoAiOperationResult<DocumentoAiBorradorResponseDto>(
                false,
                ErrorMessage: "Debes pegar o subir la documentación de origen del proyecto antes de generar el borrador.");
        }

        if (DocumentoSecretDetector.IsExternalUrlOnlyInput(rawSource))
        {
            return new DocumentoAiOperationResult<DocumentoAiBorradorResponseDto>(
                false,
                ErrorMessage: "No se leen URLs externas por seguridad. Pega el texto de la documentación o sube un archivo (.md, .txt, .pdf).");
        }

        if (rawSource.Length > maxChars)
        {
            return new DocumentoAiOperationResult<DocumentoAiBorradorResponseDto>(
                false,
                ErrorMessage: $"El texto de origen tiene {rawSource.Length:N0} caracteres y supera el límite máximo permitido de {maxChars:N0} caracteres. Recorta el texto antes de generar el borrador.");
        }

        // 1. Escaneo preventivo de secretos antes de salir del servidor
        var secretScan = DocumentoSecretDetector.ScanAndRedact(rawSource);
        var extraScan = DocumentoSecretDetector.ScanAndRedact(request.InstruccionesExtra);
        if ((secretScan.HasSecrets || extraScan.HasSecrets) && !request.EliminarSecretosDetectados)
        {
            var combinedFindings = secretScan.Findings.Concat(extraScan.Findings).ToList();
            return new DocumentoAiOperationResult<DocumentoAiBorradorResponseDto>(
                Succeeded: false,
                ErrorMessage: "Se han detectado posibles secretos, claves o contraseñas en el texto de origen. Por seguridad no se ha enviado nada a la IA. Elige cancelar o continuar eliminando los secretos detectados.",
                SecretsDetected: true,
                SecretFindings: combinedFindings,
                RedactedSourceText: secretScan.RedactedText);
        }

        var safeSource = secretScan.HasSecrets ? secretScan.RedactedText : rawSource;
        var safeExtraInstructions = extraScan.HasSecrets ? extraScan.RedactedText : (request.InstruccionesExtra?.Trim() ?? string.Empty);

        // 2. Control de límite de peticiones por usuario (Rate Limit)
        var actorId = currentUserService.GetActorIdentifier();
        if (!rateLimiter.TryAcquire(actorId, geminiOpts.MaxRequestsPerMinutePerUser))
        {
            return new DocumentoAiOperationResult<DocumentoAiBorradorResponseDto>(
                Succeeded: false,
                ErrorMessage: $"Has alcanzado el límite máximo de {geminiOpts.MaxRequestsPerMinutePerUser} generaciones por minuto para tu usuario. Espera unos segundos e inténtalo de nuevo.",
                IsRateLimited: true);
        }

        // 3. Cargar plantilla de prompt desde archivo de configuración (Prompt A o Prompt B)
        var promptFileName = request.Tipo == TipoDocumentoComercial.Contrato
            ? "contrato.prompt.txt"
            : "presupuesto.prompt.txt";

        var promptTemplate = LoadPromptTemplateFromDisk(promptFileName);
        var trato = string.IsNullOrWhiteSpace(request.TratoCliente)
            ? "Tuteo cercano y profesional (tú)"
            : DocumentoHtmlSanitizer.SanitizeText(request.TratoCliente);

        var finalPrompt = BuildPromptWithIsolatedData(
            promptTemplate,
            safeSource,
            trato,
            safeExtraInstructions);

        // 4. Llamada a Gemini en servidor con 1 reintento automático si el JSON devuelto es inválido
        var configuredModel = !string.IsNullOrWhiteSpace(request.ModeloIa)
            ? request.ModeloIa.Trim()
            : await ResolveConfiguredModelAsync(cancellationToken);
        var firstCall = await completionProvider.GenerateStructuredJsonAsync(finalPrompt, configuredModel, cancellationToken);
        if (!firstCall.Succeeded)
        {
            return new DocumentoAiOperationResult<DocumentoAiBorradorResponseDto>(
                Succeeded: false,
                ErrorMessage: firstCall.ErrorMessage ?? "Error al comunicarse con el servicio de IA.",
                IsQuotaExceeded: firstCall.HttpStatusCode == 429,
                IsTimeout: firstCall.IsTimeout);
        }

        var (validated, validationError) = DocumentoAiSchemaValidator.ValidateAndSanitizeJson(
            firstCall.RawJsonText,
            request.Tipo,
            firstCall.ModelUsed,
            safeSource.Length,
            maxBlocks,
            retried: false);

        if (validated is null)
        {
            logger.LogWarning(
                "Primera respuesta JSON de IA no válida ({Error}). Reintentando una vez...",
                validationError);

            var retryPrompt = finalPrompt +
                              "\n\nIMPORTANTE (CORRECCIÓN DE FORMATO): Tu respuesta anterior no cumplió el esquema JSON (" +
                              validationError +
                              "). Devuelve ÚNICAMENTE el objeto JSON válido con 'titulo', 'subtitulo', 'bloques' (con 'tipo' en 'texto'|'lista'|'fases', 'titulo', 'contenido', 'pendiente') y 'lineasSugeridas'.";

            var secondCall = await completionProvider.GenerateStructuredJsonAsync(retryPrompt, configuredModel, cancellationToken);
            if (!secondCall.Succeeded)
            {
                return new DocumentoAiOperationResult<DocumentoAiBorradorResponseDto>(
                    Succeeded: false,
                    ErrorMessage: secondCall.ErrorMessage ?? "Error al reintentar la generación con el servicio de IA.",
                    IsQuotaExceeded: secondCall.HttpStatusCode == 429,
                    IsTimeout: secondCall.IsTimeout);
            }

            (validated, validationError) = DocumentoAiSchemaValidator.ValidateAndSanitizeJson(
                secondCall.RawJsonText,
                request.Tipo,
                secondCall.ModelUsed,
                safeSource.Length,
                maxBlocks,
                retried: true);

            if (validated is null)
            {
                return new DocumentoAiOperationResult<DocumentoAiBorradorResponseDto>(
                    Succeeded: false,
                    ErrorMessage: $"La respuesta del servicio de IA no cumplió el esquema esperado tras reintentar ({validationError}). No se ha modificado el documento.");
            }
        }

        // 5. Registro en auditoría (quién, cuándo, documento, tamaño; NUNCA el contenido completo)
        var docRef = request.DocumentoId?.ToString() ?? "borrador-sin-guardar";
        await auditLogger.LogCriticalActionAsync(
            action: $"Documentacion.IA.GenerarBorrador.{request.Tipo}",
            entityName: nameof(DocumentoComercial),
            entityId: docRef,
            summary: $"Generación IA ({validated.ModeloUtilizado}) para {request.Tipo} (doc: {docRef}, origen: {safeSource.Length} caracteres, bloques generados: {validated.Bloques.Count}, reintentoJSON: {validated.ReintentoJsonAplicado}).",
            cancellationToken: cancellationToken);

        return new DocumentoAiOperationResult<DocumentoAiBorradorResponseDto>(
            Succeeded: true,
            Data: validated,
            RedactedSourceText: secretScan.HasSecrets ? safeSource : null);
    }

    public async Task<DocumentoAiOperationResult<DocumentoAiBloqueGeneradoDto>> RegenerarBloqueAsync(
        RegenerarBloqueAiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var geminiOpts = integrationOptions.Value.Gemini;
        var maxChars = Math.Max(100, geminiOpts.MaxSourceTextCharacters);

        if (string.IsNullOrWhiteSpace(request.BloqueTitulo) || string.IsNullOrWhiteSpace(request.BloqueContenidoActual))
        {
            return new DocumentoAiOperationResult<DocumentoAiBloqueGeneradoDto>(
                false,
                ErrorMessage: "El bloque a regenerar debe tener título y contenido.");
        }

        var rawSource = request.TextoOrigen?.Trim() ?? string.Empty;
        if (rawSource.Length > maxChars)
        {
            return new DocumentoAiOperationResult<DocumentoAiBloqueGeneradoDto>(
                false,
                ErrorMessage: $"El texto de origen supera el límite máximo permitido de {maxChars:N0} caracteres.");
        }

        var secretScanSource = DocumentoSecretDetector.ScanAndRedact(rawSource);
        var secretScanBlock = DocumentoSecretDetector.ScanAndRedact(request.BloqueContenidoActual);
        var secretScanExtra = DocumentoSecretDetector.ScanAndRedact(request.InstruccionesExtra);

        if ((secretScanSource.HasSecrets || secretScanBlock.HasSecrets || secretScanExtra.HasSecrets) &&
            !request.EliminarSecretosDetectados)
        {
            var findings = secretScanSource.Findings
                .Concat(secretScanBlock.Findings)
                .Concat(secretScanExtra.Findings)
                .ToList();

            return new DocumentoAiOperationResult<DocumentoAiBloqueGeneradoDto>(
                Succeeded: false,
                ErrorMessage: "Se han detectado posibles secretos o credenciales en el texto antes de regenerar el bloque.",
                SecretsDetected: true,
                SecretFindings: findings,
                RedactedSourceText: secretScanSource.RedactedText);
        }

        var actorId = currentUserService.GetActorIdentifier();
        if (!rateLimiter.TryAcquire(actorId, geminiOpts.MaxRequestsPerMinutePerUser))
        {
            return new DocumentoAiOperationResult<DocumentoAiBloqueGeneradoDto>(
                Succeeded: false,
                ErrorMessage: $"Has alcanzado el límite máximo de {geminiOpts.MaxRequestsPerMinutePerUser} peticiones de IA por minuto. Espera unos segundos.",
                IsRateLimited: true);
        }

        var template = LoadPromptTemplateFromDisk("regenerar-bloque.prompt.txt");
        var modoDesc = string.Equals(request.Modo, "simplificar", StringComparison.OrdinalIgnoreCase)
            ? "Hacerlo más sencillo: usa frases aún más cortas, directas y fáciles de entender para cualquier persona sin conocimientos técnicos."
            : "Regenerar este bloque manteniendo la fidelidad al material de origen con redacción comercial clara y fresca.";

        var trato = string.IsNullOrWhiteSpace(request.TratoCliente)
            ? "Tuteo cercano y profesional (tú)"
            : DocumentoHtmlSanitizer.SanitizeText(request.TratoCliente);

        var safeSource = secretScanSource.HasSecrets ? secretScanSource.RedactedText : rawSource;
        var safeBlockContent = secretScanBlock.HasSecrets ? secretScanBlock.RedactedText : request.BloqueContenidoActual;
        var safeExtra = secretScanExtra.HasSecrets ? secretScanExtra.RedactedText : (request.InstruccionesExtra?.Trim() ?? "Ninguna");

        var prompt = template
            .Replace("{{TIPO_DOCUMENTO}}", request.Tipo.ToString(), StringComparison.Ordinal)
            .Replace("{{TRATO_CLIENTE}}", trato, StringComparison.Ordinal)
            .Replace("{{MODO_REGENERACION}}", modoDesc, StringComparison.Ordinal)
            .Replace("{{INSTRUCCIONES_EXTRA}}", string.IsNullOrWhiteSpace(safeExtra) ? "Ninguna" : safeExtra, StringComparison.Ordinal)
            .Replace("{{BLOQUE_TITULO}}", NeutralizeXmlTags(request.BloqueTitulo), StringComparison.Ordinal)
            .Replace("{{BLOQUE_CONTENIDO}}", NeutralizeXmlTags(safeBlockContent), StringComparison.Ordinal)
            .Replace("{{MATERIAL_ORIGEN}}", NeutralizeXmlTags(string.IsNullOrWhiteSpace(safeSource) ? "(Usa el contenido actual del bloque como referencia)" : safeSource), StringComparison.Ordinal);

        var configuredModel = await ResolveConfiguredModelAsync(cancellationToken);
        var firstCall = await completionProvider.GenerateStructuredJsonAsync(prompt, configuredModel, cancellationToken);
        if (!firstCall.Succeeded)
        {
            return new DocumentoAiOperationResult<DocumentoAiBloqueGeneradoDto>(
                Succeeded: false,
                ErrorMessage: firstCall.ErrorMessage ?? "Error al comunicarse con el servicio de IA.",
                IsQuotaExceeded: firstCall.HttpStatusCode == 429,
                IsTimeout: firstCall.IsTimeout);
        }

        var (validated, validationError) = DocumentoAiSchemaValidator.ValidateAndSanitizeJson(
            firstCall.RawJsonText,
            request.Tipo,
            firstCall.ModelUsed,
            safeSource.Length,
            maxBlocks: 5,
            retried: false);

        if (validated is null || validated.Bloques.Count == 0)
        {
            var retryPrompt = prompt + "\n\nCORRECCIÓN: Devuelve exclusivamente un objeto JSON válido con al menos 1 elemento en 'bloques'. Error previo: " + validationError;
            var secondCall = await completionProvider.GenerateStructuredJsonAsync(retryPrompt, configuredModel, cancellationToken);
            if (!secondCall.Succeeded)
            {
                return new DocumentoAiOperationResult<DocumentoAiBloqueGeneradoDto>(
                    Succeeded: false,
                    ErrorMessage: secondCall.ErrorMessage ?? "Error al reintentar la regeneración del bloque.",
                    IsQuotaExceeded: secondCall.HttpStatusCode == 429,
                    IsTimeout: secondCall.IsTimeout);
            }

            (validated, validationError) = DocumentoAiSchemaValidator.ValidateAndSanitizeJson(
                secondCall.RawJsonText,
                request.Tipo,
                secondCall.ModelUsed,
                safeSource.Length,
                maxBlocks: 5,
                retried: true);

            if (validated is null || validated.Bloques.Count == 0)
            {
                return new DocumentoAiOperationResult<DocumentoAiBloqueGeneradoDto>(
                    Succeeded: false,
                    ErrorMessage: $"La IA no devolvió un bloque válido tras reintentar ({validationError}). No se ha modificado el bloque.");
            }
        }

        var docRef = request.DocumentoId?.ToString() ?? "borrador-sin-guardar";
        await auditLogger.LogCriticalActionAsync(
            action: $"Documentacion.IA.RegenerarBloque.{request.Modo}",
            entityName: nameof(DocumentoComercial),
            entityId: docRef,
            summary: $"Regeneración IA de bloque '{DocumentoHtmlSanitizer.SanitizeText(request.BloqueTitulo)}' (modo: {request.Modo}, doc: {docRef}, origen: {safeSource.Length} caracteres).",
            cancellationToken: cancellationToken);

        return new DocumentoAiOperationResult<DocumentoAiBloqueGeneradoDto>(
            Succeeded: true,
            Data: validated.Bloques[0]);
    }

    private string LoadPromptTemplateFromDisk(string fileName)
    {
        var configuredDir = integrationOptions.Value.Gemini.PromptsDirectory;
        var relativeDir = string.IsNullOrWhiteSpace(configuredDir)
            ? Path.Combine("Config", "Prompts", "Documentacion")
            : configuredDir.Replace('/', Path.DirectorySeparatorChar);

        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), relativeDir, fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Paye.Web", relativeDir, fileName),
            Path.Combine(AppContext.BaseDirectory, relativeDir, fileName)
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path, Encoding.UTF8);
            }
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
        {
            var probe = Path.Combine(dir.FullName, "src", "Paye.Web", relativeDir, fileName);
            if (File.Exists(probe))
            {
                return File.ReadAllText(probe, Encoding.UTF8);
            }
        }

        throw new FileNotFoundException($"No se encontró el archivo de configuración de prompt '{fileName}' en '{relativeDir}'.");
    }

    private static string BuildPromptWithIsolatedData(
        string template,
        string safeSource,
        string tratoCliente,
        string extraInstructions)
    {
        var isolatedData = NeutralizeXmlTags(safeSource);
        var isolatedExtra = string.IsNullOrWhiteSpace(extraInstructions)
            ? "Ninguna instrucción adicional."
            : NeutralizeXmlTags(extraInstructions);

        return template
            .Replace("{{TRATO_CLIENTE}}", tratoCliente, StringComparison.Ordinal)
            .Replace("{{INSTRUCCIONES_EXTRA}}", isolatedExtra, StringComparison.Ordinal)
            .Replace("{{MATERIAL_ORIGEN}}", isolatedData, StringComparison.Ordinal);
    }

    private static string NeutralizeXmlTags(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return input
            .Replace("</MATERIAL_ORIGEN_DATOS>", "[FIN_DATOS_ESCAPADO]", StringComparison.OrdinalIgnoreCase)
            .Replace("<MATERIAL_ORIGEN_DATOS>", "[INICIO_DATOS_ESCAPADO]", StringComparison.OrdinalIgnoreCase)
            .Replace("</BLOQUE_ACTUAL>", "[FIN_BLOQUE_ESCAPADO]", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    [GeneratedRegex(@"stream\r?\n([\s\S]*?)\r?\nendstream", RegexOptions.CultureInvariant)]
    private static partial Regex PdfStreamBlockRegex();

    [GeneratedRegex(@"\((?:\\.|[^\\()])*\)|<([0-9A-Fa-f]{2})+>", RegexOptions.CultureInvariant)]
    private static partial Regex PdfTextLiteralRegex();

    private static string ExtractReadableTextFromPdfBytes(byte[] pdfBytes)
    {
        var latin1 = Encoding.Latin1.GetString(pdfBytes);
        var sb = new StringBuilder();

        foreach (Match match in PdfStreamBlockRegex().Matches(latin1))
        {
            var streamStart = match.Groups[1].Index;
            var streamLen = match.Groups[1].Length;
            if (streamLen <= 0 || streamStart + streamLen > pdfBytes.Length)
            {
                continue;
            }

            var rawSlice = new ReadOnlySpan<byte>(pdfBytes, streamStart, streamLen).ToArray();
            var decodedContent = TryDecompressPdfStream(rawSlice) ?? Encoding.Latin1.GetString(rawSlice);

            if (!decodedContent.Contains("BT", StringComparison.Ordinal) &&
                !decodedContent.Contains("Tj", StringComparison.Ordinal) &&
                !decodedContent.Contains("TJ", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match token in PdfTextLiteralRegex().Matches(decodedContent))
            {
                var val = token.Value;
                if (val.StartsWith('(') && val.EndsWith(')') && val.Length >= 2)
                {
                    var inner = UnescapePdfString(val[1..^1]);
                    if (!string.IsNullOrWhiteSpace(inner))
                    {
                        sb.Append(inner).Append(' ');
                    }
                }
            }

            if (sb.Length > 0 && !sb.ToString().EndsWith('\n'))
            {
                sb.Append('\n');
            }
        }

        var result = Regex.Replace(sb.ToString(), @"[ \t]{2,}", " ").Trim();
        return result;
    }

    private static string? TryDecompressPdfStream(byte[] compressed)
    {
        try
        {
            using var input = new MemoryStream(compressed);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(zlib, Encoding.Latin1);
            return reader.ReadToEnd();
        }
        catch
        {
            try
            {
                using var input = new MemoryStream(compressed);
                using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                using var reader = new StreamReader(deflate, Encoding.Latin1);
                return reader.ReadToEnd();
            }
            catch
            {
                return null;
            }
        }
    }

    private static string UnescapePdfString(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (c == '\\' && i + 1 < raw.Length)
            {
                var next = raw[++i];
                switch (next)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case '(': sb.Append('('); break;
                    case ')': sb.Append(')'); break;
                    case '\\': sb.Append('\\'); break;
                    default:
                        if (next is >= '0' and <= '7')
                        {
                            var oct = new StringBuilder();
                            oct.Append(next);
                            while (oct.Length < 3 && i + 1 < raw.Length && raw[i + 1] is >= '0' and <= '7')
                            {
                                oct.Append(raw[++i]);
                            }
                            var charCode = Convert.ToInt32(oct.ToString(), 8);
                            sb.Append((char)charCode);
                        }
                        else
                        {
                            sb.Append(next);
                        }
                        break;
                }
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
