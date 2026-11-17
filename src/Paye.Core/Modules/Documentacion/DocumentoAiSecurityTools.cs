using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Paye.Core.Modules.Documentacion;

public sealed record DocumentoSecretFindingDto(
    string Category,
    string Description,
    string MaskedPreview);

public sealed record DocumentoSecretScanResult(
    bool HasSecrets,
    IReadOnlyList<DocumentoSecretFindingDto> Findings,
    string RedactedText);

public sealed record DocumentoAiBloqueGeneradoDto(
    string Tipo, // "texto" | "lista" | "fases"
    string Titulo,
    string Contenido,
    IReadOnlyList<string> Pendiente);

public sealed record DocumentoAiLineaSugeridaDto(
    string Descripcion,
    decimal Cantidad);

public sealed record DocumentoAiBorradorResponseDto(
    string Titulo,
    string Subtitulo,
    IReadOnlyList<DocumentoAiBloqueGeneradoDto> Bloques,
    IReadOnlyList<DocumentoAiLineaSugeridaDto> LineasSugeridas,
    string ModeloUtilizado,
    int CaracteresOrigenProcesados,
    bool ReintentoJsonAplicado = false);

public static partial class DocumentoSecretDetector
{
    public const string RedactedPlaceholder = "[SECRETO_ELIMINADO]";

    [GeneratedRegex(@"-----BEGIN (?:RSA |EC |DSA |OPENSSH |ENCRYPTED )?PRIVATE KEY-----[\s\S]*?-----END (?:RSA |EC |DSA |OPENSSH |ENCRYPTED )?PRIVATE KEY-----", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PemPrivateKeyRegex();

    [GeneratedRegex(@"\b(AIza[0-9A-Za-z\-_]{25,}|sk-(?:proj-|live-|test-)?[0-9A-Za-z\-_]{20,}|gh[pousr]_[0-9A-Za-z]{20,}|glpat-[0-9A-Za-z\-_]{15,}|xox[baprs]-[0-9A-Za-z\-]{15,}|AKIA[0-9A-Z]{16})\b", RegexOptions.CultureInvariant)]
    private static partial Regex ApiKeyTokenRegex();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex JwtTokenRegex();

    [GeneratedRegex(@"(?im)^(\s*[A-Z0-9_]*(?:PASSWORD|PASSWD|SECRET|API_KEY|APIKEY|PRIVATE_KEY|ACCESS_TOKEN|AUTH_TOKEN|CLIENT_SECRET|CONNECTIONSTRING|DATABASE_URL)[A-Z0-9_]*\s*=\s*)([^\r\n#]{4,})$", RegexOptions.CultureInvariant)]
    private static partial Regex EnvSecretAssignmentRegex();

    [GeneratedRegex(@"(?i)((?:Password|Pwd|ApiKey|ClientSecret|SecretKey)\s*=\s*)([^;\r\n'""\s]{4,})", RegexOptions.CultureInvariant)]
    private static partial Regex InlineConnectionSecretRegex();

    [GeneratedRegex(@"(?i)((?:postgres|postgresql|mysql|mongodb|redis|amqp)://[^:\s/]+:)([^@\s]+)(@)", RegexOptions.CultureInvariant)]
    private static partial Regex UriEmbeddedPasswordRegex();

    public static DocumentoSecretScanResult ScanAndRedact(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new DocumentoSecretScanResult(false, [], string.Empty);
        }

        var findings = new List<DocumentoSecretFindingDto>();
        var redacted = input;

        // 1. Claves privadas PEM
        redacted = PemPrivateKeyRegex().Replace(redacted, match =>
        {
            findings.Add(new DocumentoSecretFindingDto(
                "Clave privada",
                "Bloque de clave privada criptográfica (PEM/SSH)",
                "-----BEGIN PRIVATE KEY----- ..."));
            return RedactedPlaceholder;
        });

        // 2. Claves de API conocidas (Gemini, OpenAI, GitHub, GitLab, Slack, AWS)
        redacted = ApiKeyTokenRegex().Replace(redacted, match =>
        {
            var val = match.Value;
            var prefix = val.Length >= 6 ? val[..6] : "key";
            findings.Add(new DocumentoSecretFindingDto(
                "Clave de API / Token",
                "Clave de API o token de proveedor externo detectado",
                $"{prefix}***"));
            return RedactedPlaceholder;
        });

        // 3. Tokens JWT
        redacted = JwtTokenRegex().Replace(redacted, _ =>
        {
            findings.Add(new DocumentoSecretFindingDto(
                "Token JWT",
                "Token portador en formato JSON Web Token (eyJ...)",
                "eyJ***.eyJ***"));
            return RedactedPlaceholder;
        });

        // 4. Variables de entorno (.env) con secretos o contraseñas
        redacted = EnvSecretAssignmentRegex().Replace(redacted, match =>
        {
            var keyPart = match.Groups[1].Value.Trim();
            var valuePart = match.Groups[2].Value.Trim();
            if (IsPlaceholderValue(valuePart))
            {
                return match.Value;
            }

            findings.Add(new DocumentoSecretFindingDto(
                "Variable .env sensible",
                $"Asignación de variable sensible en texto ({keyPart.TrimEnd('=', ' ')})",
                $"{keyPart}***"));
            return $"{match.Groups[1].Value}{RedactedPlaceholder}";
        });

        // 5. Cadenas de conexión con Password=... o ApiKey=...
        redacted = InlineConnectionSecretRegex().Replace(redacted, match =>
        {
            var val = match.Groups[2].Value.Trim();
            if (IsPlaceholderValue(val))
            {
                return match.Value;
            }

            findings.Add(new DocumentoSecretFindingDto(
                "Credencial en cadena de conexión",
                "Contraseña o secreto en parámetro de conexión",
                $"{match.Groups[1].Value}***"));
            return $"{match.Groups[1].Value}{RedactedPlaceholder}";
        });

        // 6. URLs de base de datos con contraseña embebida (postgres://user:pass@host)
        redacted = UriEmbeddedPasswordRegex().Replace(redacted, match =>
        {
            var val = match.Groups[2].Value.Trim();
            if (IsPlaceholderValue(val))
            {
                return match.Value;
            }

            findings.Add(new DocumentoSecretFindingDto(
                "URI con credenciales",
                "Contraseña embebida en URI de conexión",
                $"{match.Groups[1].Value}***@"));
            return $"{match.Groups[1].Value}{RedactedPlaceholder}{match.Groups[3].Value}";
        });

        return new DocumentoSecretScanResult(
            HasSecrets: findings.Count > 0,
            Findings: findings,
            RedactedText: redacted);
    }

    public static bool IsExternalUrlOnlyInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (trimmed.Contains('\n') || trimmed.Contains(' '))
        {
            return false;
        }

        return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlaceholderValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        return value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("TU_CLAVE", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("SECRETO_ELIMINADO", StringComparison.OrdinalIgnoreCase) ||
               value.All(c => c == '*');
    }
}

public static class DocumentoAiSchemaValidator
{
    private static readonly HashSet<string> AllowedBlockTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "texto",
        "lista",
        "fases"
    };

    private static readonly string[] ForbiddenContractLegalKeywords =
    [
        "protección de datos",
        "proteccion de datos",
        "rgpd",
        "lopdgdd",
        "propiedad intelectual",
        "confidencialidad",
        "jurisdicción",
        "jurisdiccion",
        "fuero aplicable",
        "penalizaciones",
        "resolución anticipada",
        "resolucion anticipada"
    ];

    public static (DocumentoAiBorradorResponseDto? Result, string? ErrorMessage) ValidateAndSanitizeJson(
        string? rawModelOutput,
        TipoDocumentoComercial tipoDocumento,
        string modelName,
        int sourceLengthChars,
        int maxBlocks = 15,
        bool retried = false)
    {
        if (string.IsNullOrWhiteSpace(rawModelOutput))
        {
            return (null, "La respuesta del modelo de IA está vacía.");
        }

        var cleanJson = StripMarkdownCodeFences(rawModelOutput.Trim());

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(cleanJson, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 16
            });
        }
        catch (JsonException)
        {
            return (null, "La respuesta del modelo no es un JSON válido conforme al esquema exigido.");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (null, "El elemento raíz de la respuesta debe ser un objeto JSON.");
            }

            if (!TryGetStringProperty(root, "titulo", out var rawTitulo) || string.IsNullOrWhiteSpace(rawTitulo))
            {
                return (null, "Falta el campo obligatorio 'titulo' en el JSON de salida.");
            }

            if (!TryGetStringProperty(root, "subtitulo", out var rawSubtitulo))
            {
                return (null, "Falta el campo obligatorio 'subtitulo' en el JSON de salida.");
            }

            if (!TryGetPropertyCaseInsensitive(root, "bloques", out var bloquesElement) ||
                bloquesElement.ValueKind != JsonValueKind.Array)
            {
                return (null, "Falta el array obligatorio 'bloques' en el JSON de salida.");
            }

            var blockCount = bloquesElement.GetArrayLength();
            if (blockCount == 0)
            {
                return (null, "El array 'bloques' no puede estar vacío.");
            }

            if (blockCount > maxBlocks)
            {
                return (null, $"El modelo devolvió {blockCount} bloques, superando el máximo permitido ({maxBlocks}).");
            }

            var validatedBlocks = new List<DocumentoAiBloqueGeneradoDto>(blockCount);
            foreach (var item in bloquesElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    return (null, "Cada elemento de 'bloques' debe ser un objeto JSON.");
                }

                if (!TryGetStringProperty(item, "tipo", out var rawTipo) ||
                    string.IsNullOrWhiteSpace(rawTipo) ||
                    !AllowedBlockTypes.Contains(rawTipo.Trim()))
                {
                    return (null, $"Tipo de bloque inválido '{rawTipo}'. Solo se admiten 'texto', 'lista' o 'fases'.");
                }

                if (!TryGetStringProperty(item, "titulo", out var rawBlkTitulo) ||
                    string.IsNullOrWhiteSpace(rawBlkTitulo))
                {
                    return (null, "Todos los bloques deben incluir un 'titulo' no vacío.");
                }

                if (!TryGetStringProperty(item, "contenido", out var rawContenido) ||
                    string.IsNullOrWhiteSpace(rawContenido))
                {
                    return (null, $"El bloque '{rawBlkTitulo}' tiene el campo 'contenido' vacío.");
                }

                if (rawContenido.Length > 15_000)
                {
                    return (null, $"El contenido del bloque '{rawBlkTitulo}' supera la longitud máxima permitida (15.000 caracteres).");
                }

                var pendientes = new List<string>();
                if (TryGetPropertyCaseInsensitive(item, "pendiente", out var pendienteElement))
                {
                    if (pendienteElement.ValueKind != JsonValueKind.Array)
                    {
                        return (null, $"El campo 'pendiente' del bloque '{rawBlkTitulo}' debe ser un array de textos.");
                    }

                    foreach (var p in pendienteElement.EnumerateArray())
                    {
                        if (p.ValueKind != JsonValueKind.String)
                        {
                            return (null, "Todos los elementos de 'pendiente' deben ser cadenas de texto.");
                        }

                        var cleanP = Truncate(DocumentoHtmlSanitizer.SanitizeText(p.GetString()), 300);
                        if (!string.IsNullOrWhiteSpace(cleanP) && pendientes.Count < 15)
                        {
                            pendientes.Add(cleanP);
                        }
                    }
                }

                var cleanBlkTitulo = Truncate(DocumentoHtmlSanitizer.SanitizeText(rawBlkTitulo), 160);
                var cleanBlkContenido = DocumentoHtmlSanitizer.SanitizeHtml(rawContenido);

                // Regla de seguridad de Contratos: nunca generar ni modificar cláusulas legales de plantilla
                if (tipoDocumento == TipoDocumentoComercial.Contrato &&
                    IsForbiddenLegalClauseTitle(cleanBlkTitulo))
                {
                    cleanBlkContenido = $"<p><strong>[Revisar con asesor:</strong> el material de origen hacía referencia a aspectos de «{cleanBlkTitulo}». Se conserva intacta la cláusula legal oficial de la plantilla del CRM.]</p>";
                    if (!pendientes.Any(p => p.Contains("Revisar con asesor", StringComparison.OrdinalIgnoreCase)))
                    {
                        pendientes.Add($"[Revisar con asesor: referencia a {cleanBlkTitulo}]");
                    }
                }

                validatedBlocks.Add(new DocumentoAiBloqueGeneradoDto(
                    Tipo: rawTipo.Trim().ToLowerInvariant(),
                    Titulo: cleanBlkTitulo,
                    Contenido: cleanBlkContenido,
                    Pendiente: pendientes));
            }

            var validatedLines = new List<DocumentoAiLineaSugeridaDto>();
            if (TryGetPropertyCaseInsensitive(root, "lineasSugeridas", out var lineasElement))
            {
                if (lineasElement.ValueKind != JsonValueKind.Array)
                {
                    return (null, "El campo 'lineasSugeridas' debe ser un array JSON.");
                }

                if (lineasElement.GetArrayLength() > 30)
                {
                    return (null, "El campo 'lineasSugeridas' supera el límite de 30 líneas.");
                }

                foreach (var lineItem in lineasElement.EnumerateArray())
                {
                    if (lineItem.ValueKind != JsonValueKind.Object)
                    {
                        return (null, "Cada elemento de 'lineasSugeridas' debe ser un objeto JSON.");
                    }

                    if (!TryGetStringProperty(lineItem, "descripcion", out var rawDesc) ||
                        string.IsNullOrWhiteSpace(rawDesc))
                    {
                        return (null, "Cada línea sugerida debe incluir una 'descripcion' no vacía.");
                    }

                    decimal cantidad = 1m;
                    if (TryGetPropertyCaseInsensitive(lineItem, "cantidad", out var cantEl))
                    {
                        if (cantEl.ValueKind != JsonValueKind.Number || !cantEl.TryGetDecimal(out cantidad))
                        {
                            return (null, $"La cantidad de la línea '{rawDesc}' debe ser un número válido.");
                        }
                    }

                    if (cantidad <= 0m || cantidad > 100_000m)
                    {
                        cantidad = 1m;
                    }

                    var cleanDesc = Truncate(DocumentoHtmlSanitizer.SanitizeText(rawDesc), 500);
                    if (!string.IsNullOrWhiteSpace(cleanDesc))
                    {
                        validatedLines.Add(new DocumentoAiLineaSugeridaDto(
                            Descripcion: cleanDesc,
                            Cantidad: decimal.Round(cantidad, 2, MidpointRounding.AwayFromZero)));
                    }
                }
            }

            var cleanTitulo = Truncate(DocumentoHtmlSanitizer.SanitizeText(rawTitulo), 200);
            var cleanSubtitulo = Truncate(DocumentoHtmlSanitizer.SanitizeText(rawSubtitulo), 250);

            return (new DocumentoAiBorradorResponseDto(
                Titulo: cleanTitulo,
                Subtitulo: cleanSubtitulo,
                Bloques: validatedBlocks,
                LineasSugeridas: validatedLines,
                ModeloUtilizado: modelName,
                CaracteresOrigenProcesados: sourceLengthChars,
                ReintentoJsonAplicado: retried), null);
        }
    }

    private static bool IsForbiddenLegalClauseTitle(string title)
    {
        var lower = title.ToLowerInvariant();
        foreach (var keyword in ForbiddenContractLegalKeywords)
        {
            if (lower.Contains(keyword, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static string StripMarkdownCodeFences(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstNewLine = text.IndexOf('\n');
        if (firstNewLine < 0)
        {
            return text.Trim('`');
        }

        var body = text[(firstNewLine + 1)..].Trim();
        if (body.EndsWith("```", StringComparison.Ordinal))
        {
            body = body[..^3].Trim();
        }

        return body;
    }

    private static bool TryGetStringProperty(JsonElement obj, string name, out string value)
    {
        value = string.Empty;
        if (!TryGetPropertyCaseInsensitive(obj, name, out var prop) || prop.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = prop.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string Truncate(string input, int maxLength)
    {
        if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
        {
            return input;
        }

        return input[..maxLength].TrimEnd();
    }
}
