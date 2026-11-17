using System.ComponentModel.DataAnnotations;

namespace Paye.Core.Modules.Documentacion;

/// <summary>
/// Bloque o cláusula individual de un Contrato o Presupuesto.
/// Permite reordenar, ocultar, editar en enriquecido ligero y restablecer a la plantilla original.
/// </summary>
public sealed record DocumentoBloqueDto(
    string Id,
    string? TemplateBlockId,
    string TipoBloque, // "pill-section" | "highlight-cost-box" | "timeline-steps" | "clause" | "signatures" | "pricing-table"
    string? NumeroPastilla,
    string Titulo,
    string ContenidoHtml,
    bool Visible,
    int Orden,
    bool EsPersonalizado);

public sealed record DocumentoLineaInputDto(
    [Required(ErrorMessage = "La descripción del concepto es obligatoria.")]
    [StringLength(500)]
    string Descripcion,

    [Range(0.0001, 1000000, ErrorMessage = "La cantidad debe ser mayor que 0.")]
    decimal Cantidad,

    [Range(0, 100000000, ErrorMessage = "El precio unitario no puede ser negativo.")]
    decimal PrecioUnitario,

    [Range(0, 100, ErrorMessage = "El descuento debe estar entre 0% y 100%.")]
    decimal DescuentoPorcentaje = 0m,

    decimal IvaPorcentaje = 21m);

public sealed record DocumentoLineaDto(
    Guid Id,
    int Orden,
    string Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal DescuentoPorcentaje,
    decimal IvaPorcentaje,
    decimal ImporteBruto,
    decimal ImporteDescuento,
    decimal BaseImponible,
    decimal CuotaIva,
    decimal TotalLinea);

public sealed record DesgloseIvaItemDto(
    decimal IvaPorcentaje,
    string Etiqueta,
    decimal BaseImponible,
    decimal CuotaIva,
    decimal TotalConIva);

public sealed record DocumentoComercialListItemDto(
    Guid Id,
    TipoDocumentoComercial Tipo,
    string NumeroDocumento,
    string Titulo,
    string Subtitulo,
    Guid? ClienteId,
    string ClienteNombre,
    string ClienteCifNif,
    DateOnly FechaEmision,
    DateOnly? FechaValidez,
    decimal BaseImponible,
    decimal TotalIva,
    decimal TotalGeneral,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    uint RowVersion);

public sealed record DocumentoComercialDetailDto(
    Guid Id,
    TipoDocumentoComercial Tipo,
    int FiscalYear,
    int SequentialNumber,
    string NumeroDocumento,
    string Titulo,
    string Subtitulo,
    string Categoria,
    Guid? ClienteId,
    string ClienteNombre,
    string ClienteCifNif,
    string ClienteDireccion,
    string ClienteEmail,
    string ClienteTelefono,
    DateOnly FechaEmision,
    DateOnly? FechaValidez,
    string? ObjetoContrato,
    string? DuracionContrato,
    string? PrecioTextoContrato,
    string? FormaPagoContrato,
    string? LugarFirma,
    IReadOnlyList<DocumentoBloqueDto> Bloques,
    IReadOnlyList<DocumentoLineaDto> Lineas,
    IReadOnlyList<DesgloseIvaItemDto> DesgloseIva,
    decimal SubtotalBruto,
    decimal TotalDescuento,
    decimal BaseImponible,
    decimal TotalIva,
    decimal TotalGeneral,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    uint RowVersion,
    string? TextoOrigenDatos = null,
    string? NombreArchivoOrigen = null);

public sealed record SaveDocumentoComercialRequest
{
    [Required]
    public TipoDocumentoComercial Tipo { get; init; }

    /// <summary>
    /// Opcional: permite crear y guardar presupuestos y contratos sin ningún cliente del CRM.
    /// </summary>
    public Guid? ClienteId { get; init; }

    [StringLength(200)]
    public string? Titulo { get; init; }

    [StringLength(250)]
    public string? Subtitulo { get; init; }

    [StringLength(120)]
    public string? Categoria { get; init; }

    // Permiten sobreescribir el snapshot del cliente si el usuario lo edita en el panel izquierdo
    [StringLength(200)]
    public string? ClienteNombreOverride { get; init; }

    [StringLength(30)]
    public string? ClienteCifNifOverride { get; init; }

    [StringLength(300)]
    public string? ClienteDireccionOverride { get; init; }

    [StringLength(150)]
    public string? ClienteEmailOverride { get; init; }

    [StringLength(40)]
    public string? ClienteTelefonoOverride { get; init; }

    public DateOnly? FechaEmision { get; init; }
    public DateOnly? FechaValidez { get; init; }

    [StringLength(500)]
    public string? ObjetoContrato { get; init; }

    [StringLength(200)]
    public string? DuracionContrato { get; init; }

    [StringLength(300)]
    public string? PrecioTextoContrato { get; init; }

    [StringLength(300)]
    public string? FormaPagoContrato { get; init; }

    [StringLength(150)]
    public string? LugarFirma { get; init; }

    public IReadOnlyList<DocumentoBloqueDto>? Bloques { get; init; }
    public IReadOnlyList<DocumentoLineaInputDto>? Lineas { get; init; }

    /// <summary>
    /// Texto de origen opcional (documentación del proyecto, manual, README) guardado en el documento
    /// para poder regenerar el borrador o bloques individuales más adelante.
    /// </summary>
    [StringLength(60000, ErrorMessage = "El texto de origen no puede superar los 60.000 caracteres.")]
    public string? TextoOrigenDatos { get; init; }

    [StringLength(200)]
    public string? NombreArchivoOrigen { get; init; }

    public uint RowVersion { get; init; }
}

public sealed record DocumentacionFilterQuery
{
    public TipoDocumentoComercial? Tipo { get; init; }
    public Guid? ClienteId { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record DocumentacionPagedResult(
    IReadOnlyList<DocumentoComercialListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record DocumentacionCompanySettingsDto(
    Guid Id,
    string NombreEmpresa,
    string CifNif,
    string Direccion,
    string Telefono,
    string Email,
    string Web,
    string Iban,
    string LogoUrl,
    string TextoLegalTitulo,
    string TextoLegalContenido,
    uint RowVersion,
    string ModeloIa = "gemini-3.8-flash");

public sealed record UpdateDocumentacionCompanySettingsRequest
{
    [StringLength(200)]
    public string NombreEmpresa { get; init; } = string.Empty;

    [StringLength(30)]
    public string CifNif { get; init; } = string.Empty;

    [StringLength(300)]
    public string Direccion { get; init; } = string.Empty;

    [StringLength(40)]
    public string Telefono { get; init; } = string.Empty;

    [StringLength(150)]
    public string Email { get; init; } = string.Empty;

    [StringLength(200)]
    public string Web { get; init; } = string.Empty;

    [StringLength(50)]
    public string Iban { get; init; } = string.Empty;

    [StringLength(500)]
    public string LogoUrl { get; init; } = string.Empty;

    [StringLength(150)]
    public string TextoLegalTitulo { get; init; } = "Propiedad Intelectual y Confidencialidad";

    [StringLength(2000)]
    public string TextoLegalContenido { get; init; } = string.Empty;

    [StringLength(80)]
    public string? ModeloIa { get; init; } = "gemini-3.8-flash";

    [StringLength(300)]
    public string? GeminiApiKey { get; init; }

    public uint RowVersion { get; init; }
}

public sealed record DocumentacionOperationResult<T>(
    bool Succeeded,
    T? Data = default,
    string? ErrorMessage = null,
    bool IsNotFound = false,
    bool IsConflict = false);

public static class DocumentacionOperationResult
{
    public static DocumentacionOperationResult<T> Ok<T>(T data) => new(true, data);
    public static DocumentacionOperationResult<T> Validation<T>(string message) => new(false, default, message);
    public static DocumentacionOperationResult<T> NotFound<T>(string message) => new(false, default, message, IsNotFound: true);
    public static DocumentacionOperationResult<T> Conflict<T>(string message = "El registro fue modificado por otro usuario. Recargue la página e inténtelo de nuevo.") =>
        new(false, default, message, IsConflict: true);
}

public interface IDocumentacionService
{
    Task<DocumentacionPagedResult> GetPagedAsync(
        DocumentacionFilterQuery query,
        CancellationToken cancellationToken = default);

    Task<DocumentoComercialDetailDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<DocumentacionOperationResult<DocumentoComercialDetailDto>> CreateAsync(
        SaveDocumentoComercialRequest request,
        CancellationToken cancellationToken = default);

    Task<DocumentacionOperationResult<DocumentoComercialDetailDto>> UpdateAsync(
        Guid id,
        SaveDocumentoComercialRequest request,
        CancellationToken cancellationToken = default);

    Task<DocumentacionOperationResult<DocumentoComercialDetailDto>> DuplicateAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<DocumentacionOperationResult<bool>> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<DocumentacionCompanySettingsDto> GetCompanySettingsAsync(
        CancellationToken cancellationToken = default);

    Task<DocumentacionOperationResult<DocumentacionCompanySettingsDto>> UpdateCompanySettingsAsync(
        UpdateDocumentacionCompanySettingsRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record DocumentoA4LayoutOptionsDto(
    bool MostrarCabeceraEmpresa = false,
    bool MostrarCodigoEnPortada = false,
    bool MostrarCenefaPortada = false,
    bool MostrarDatosFiscalesPie = false,
    bool EvitarPartirBloques = true);

public sealed record DocumentoPdfExportResult(
    byte[] PdfBytes,
    string FileName,
    string HtmlContent);

public interface IDocumentoPdfExportService
{
    string BuildSelfContainedA4Html(
        DocumentoComercialDetailDto document,
        DocumentacionCompanySettingsDto company,
        DocumentoA4LayoutOptionsDto? layoutOptions = null);

    Task<DocumentacionOperationResult<DocumentoPdfExportResult>> ExportPdfByIdAsync(
        Guid id,
        DocumentoA4LayoutOptionsDto? layoutOptions = null,
        CancellationToken cancellationToken = default);
}

public sealed record GenerarBorradorAiRequest
{
    [Required]
    public TipoDocumentoComercial Tipo { get; init; }

    public Guid? DocumentoId { get; init; }

    [Required(ErrorMessage = "Debes introducir o subir el texto de origen del proyecto.")]
    public string TextoOrigen { get; init; } = string.Empty;

    [StringLength(120)]
    public string? TratoCliente { get; init; }

    [StringLength(1000)]
    public string? InstruccionesExtra { get; init; }

    [StringLength(80)]
    public string? ModeloIa { get; init; }

    public bool EliminarSecretosDetectados { get; init; }
}

public sealed record RegenerarBloqueAiRequest
{
    [Required]
    public TipoDocumentoComercial Tipo { get; init; }

    public Guid? DocumentoId { get; init; }

    [Required]
    [StringLength(200)]
    public string BloqueTitulo { get; init; } = string.Empty;

    [Required]
    [StringLength(20000)]
    public string BloqueContenidoActual { get; init; } = string.Empty;

    /// <summary>
    /// "regenerar" | "simplificar"
    /// </summary>
    [Required]
    [StringLength(40)]
    public string Modo { get; init; } = "regenerar";

    [StringLength(1000)]
    public string? InstruccionesExtra { get; init; }

    public string? TextoOrigen { get; init; }

    [StringLength(120)]
    public string? TratoCliente { get; init; }

    public bool EliminarSecretosDetectados { get; init; }
}

public sealed record EscanearSecretosAiRequest
{
    public string TextoOrigen { get; init; } = string.Empty;
}

public sealed record DocumentoArchivoExtraidoDto(
    string FileName,
    string ExtractedText,
    int CharacterCount,
    bool ExceededMaxCharacters,
    int MaxAllowedCharacters);

public sealed record DocumentoAiOperationResult<T>(
    bool Succeeded,
    T? Data = default,
    string? ErrorMessage = null,
    bool SecretsDetected = false,
    IReadOnlyList<DocumentoSecretFindingDto>? SecretFindings = null,
    string? RedactedSourceText = null,
    bool IsRateLimited = false,
    bool IsQuotaExceeded = false,
    bool IsTimeout = false);

public interface IDocumentoGeminiCompletionProvider
{
    Task<(bool Succeeded, string? RawJsonText, string ModelUsed, int HttpStatusCode, bool IsTimeout, string? ErrorMessage)> GenerateStructuredJsonAsync(
        string systemAndUserPrompt,
        string? modelOverride = null,
        CancellationToken cancellationToken = default);
}

public interface IDocumentoAiGenerationService
{
    DocumentoSecretScanResult ScanSecrets(string? textoOrigen);

    Task<DocumentoAiOperationResult<DocumentoArchivoExtraidoDto>> ExtractTextFromUploadedFileAsync(
        Stream fileStream,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default);

    Task<DocumentoAiOperationResult<DocumentoAiBorradorResponseDto>> GenerarBorradorAsync(
        GenerarBorradorAiRequest request,
        CancellationToken cancellationToken = default);

    Task<DocumentoAiOperationResult<DocumentoAiBloqueGeneradoDto>> RegenerarBloqueAsync(
        RegenerarBloqueAiRequest request,
        CancellationToken cancellationToken = default);
}
