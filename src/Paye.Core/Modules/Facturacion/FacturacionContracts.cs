using Paye.Core.Invoicing;

namespace Paye.Core.Modules.Facturacion;

public enum FacturacionOperationStatus
{
    Success,
    NotFound,
    ConcurrencyConflict,
    ValidationError,
    ForbiddenInDemo
}

public sealed record FacturacionOperationResult(
    FacturacionOperationStatus Status,
    Guid? DocumentId = null,
    string? FormattedNumber = null,
    string? ErrorMessage = null)
{
    public bool Succeeded => Status == FacturacionOperationStatus.Success;

    public static FacturacionOperationResult Ok(Guid? id = null, string? number = null) =>
        new(FacturacionOperationStatus.Success, id, number, null);

    public static FacturacionOperationResult Validation(string errorMessage) =>
        new(FacturacionOperationStatus.ValidationError, null, null, errorMessage);

    public static FacturacionOperationResult NotFoundResult(string errorMessage = "El documento solicitado no existe.") =>
        new(FacturacionOperationStatus.NotFound, null, null, errorMessage);

    public static FacturacionOperationResult Conflict(string errorMessage = "El registro fue modificado por otra operación. Recarga e inténtalo de nuevo.") =>
        new(FacturacionOperationStatus.ConcurrencyConflict, null, null, errorMessage);

    public static FacturacionOperationResult DemoBlocked(string errorMessage = "El modo demostración solo permite crear presupuestos y borradores de factura. La emisión fiscal definitiva está bloqueada.") =>
        new(FacturacionOperationStatus.ForbiddenInDemo, null, null, errorMessage);
}

public sealed record CreateTaxRateCommand(
    string Code,
    string Name,
    TaxKind Kind,
    decimal Percentage,
    Guid? ParentVatTaxId,
    TaxApplicability Applicability,
    bool IsDefault = false,
    string AeatTaxCode = "01",
    string AeatRegimeKey = "01",
    string AeatOperationQualification = "S1");

/// <summary>
/// Comando de edición de un impuesto existente en el catálogo.
/// Por regla de negocio acordada, el porcentaje, tipo y código no se modifican una vez creado:
/// solo se permite editar nombre, aplicabilidad, si está activo y si es el predeterminado.
/// </summary>
public sealed record UpdateTaxRateCommand(
    Guid Id,
    string Name,
    TaxApplicability Applicability,
    bool IsActive,
    bool IsDefault,
    uint RowVersion);

public sealed record DocumentLineInputDto(
    string Description,
    decimal Quantity,
    decimal UnitPriceNet,
    decimal VatPercentage,
    Guid? VatTaxId = null,
    Guid? SurchargeTaxId = null,
    decimal SurchargePercentage = 0m);

public sealed record DocumentLineDto(
    Guid Id,
    int LineOrder,
    string Description,
    decimal Quantity,
    decimal UnitPriceNet,
    Guid? VatTaxId,
    string VatTaxName,
    decimal VatPercentage,
    Guid? SurchargeTaxId,
    string? SurchargeTaxName,
    decimal SurchargePercentage,
    decimal LineNet);

public sealed record VatBreakdownDto(
    decimal VatPercentage,
    string VatTaxName,
    decimal TaxableBase,
    decimal TaxAmount,
    decimal SurchargePercentage,
    string? SurchargeTaxName,
    decimal SurchargeAmount,
    decimal GrossAmount);

public sealed record InvoiceTaxBreakdownDto(
    Guid Id,
    Guid? TaxId,
    TaxKind TaxKind,
    string TaxName,
    decimal TaxPercentage,
    decimal? LinkedVatPercentage,
    string AeatTaxCode,
    string AeatRegimeKey,
    string AeatOperationQualification,
    decimal TaxableBase,
    decimal TaxAmount);

public sealed record BillingSettingsDto(
    Guid Id,
    string TaxName,
    string TaxId,
    string FiscalAddress,
    Guid? LogoPrivateFileId,
    string LegalTermsText,
    string FooterText,
    uint RowVersion,
    bool IsReadyToIssue);

public sealed record UpdateBillingSettingsCommand(
    string TaxName,
    string TaxId,
    string FiscalAddress,
    Guid? LogoPrivateFileId,
    string LegalTermsText,
    string FooterText,
    uint RowVersion);

public sealed record BillingSeriesDto(
    Guid Id,
    string Code,
    string Name,
    BillingDocumentKind DocumentKind,
    int FiscalYear,
    int LastNumber,
    int PaddingDigits,
    bool IsActive,
    bool IsDefault,
    uint RowVersion);

public sealed record SaveBillingSeriesCommand(
    Guid? Id,
    string Code,
    string Name,
    BillingDocumentKind DocumentKind,
    int FiscalYear,
    int PaddingDigits,
    bool IsActive,
    bool IsDefault);

public sealed record QuoteDto(
    Guid Id,
    Guid SeriesId,
    string SeriesCode,
    int FiscalYear,
    int SequentialNumber,
    string FormattedNumber,
    QuoteStatus Status,
    DateOnly IssueDate,
    DateOnly? ValidUntilDate,
    string CustomerName,
    string? CustomerTaxId,
    string? CustomerAddress,
    Guid? WithholdingTaxId,
    string? WithholdingTaxName,
    decimal WithholdingPercentage,
    decimal SubtotalNet,
    decimal TotalVat,
    decimal TotalSurcharge,
    decimal TotalTax,
    decimal TotalGross,
    decimal TotalWithholding,
    decimal TotalPayable,
    string? Notes,
    Guid? ConvertedInvoiceId,
    uint RowVersion,
    IReadOnlyList<DocumentLineDto> Lines,
    IReadOnlyList<VatBreakdownDto> VatBreakdown);

public sealed record SaveQuoteCommand(
    Guid? Id,
    Guid? SeriesId,
    DateOnly? IssueDate,
    DateOnly? ValidUntilDate,
    string CustomerName,
    string? CustomerTaxId,
    string? CustomerAddress,
    string? Notes,
    IReadOnlyList<DocumentLineInputDto> Lines,
    Guid? WithholdingTaxId = null,
    decimal WithholdingPercentage = 0m,
    uint? RowVersion = null);

public sealed record InvoiceDto(
    Guid Id,
    InvoiceDocumentType InvoiceType,
    InvoiceStatus Status,
    Guid SeriesId,
    string SeriesCode,
    int FiscalYear,
    int? SequentialNumber,
    string? FormattedNumber,
    DateOnly IssueDate,
    DateTimeOffset? IssuedAtUtc,
    string IssuerTaxName,
    string IssuerTaxId,
    string IssuerFiscalAddress,
    string CustomerName,
    string? CustomerTaxId,
    string? CustomerAddress,
    BillingPaymentMethod PaymentMethod,
    Guid? WithholdingTaxId,
    string? WithholdingTaxName,
    decimal WithholdingPercentage,
    decimal SubtotalNet,
    decimal TotalVat,
    decimal TotalSurcharge,
    decimal TotalTax,
    decimal TotalGross,
    decimal TotalWithholding,
    decimal TotalPayable,
    string? Notes,
    Guid? SourceQuoteId,
    Guid? RectifiedInvoiceId,
    RectificationScope? RectificationScope,
    string? RectificationReason,
    uint RowVersion,
    IReadOnlyList<DocumentLineDto> Lines,
    IReadOnlyList<VatBreakdownDto> VatBreakdown,
    IReadOnlyList<InvoiceTaxBreakdownDto> AppliedTaxes);

public sealed record SaveDraftInvoiceCommand(
    Guid? Id,
    InvoiceDocumentType InvoiceType,
    Guid? SeriesId,
    DateOnly? IssueDate,
    string CustomerName,
    string? CustomerTaxId,
    string? CustomerAddress,
    BillingPaymentMethod PaymentMethod,
    string? Notes,
    IReadOnlyList<DocumentLineInputDto> Lines,
    Guid? WithholdingTaxId = null,
    decimal WithholdingPercentage = 0m,
    uint? RowVersion = null);

public sealed record PdfDocumentResult(
    bool Succeeded,
    byte[]? PdfBytes = null,
    string? FileName = null,
    string? ErrorMessage = null);

public interface IPdfInvoiceGenerator
{
    byte[] GenerateInvoicePdf(InvoiceDto invoice, BillingSettingsDto settings, byte[]? logoBytes = null);
    byte[] GenerateQuotePdf(QuoteDto quote, BillingSettingsDto settings, byte[]? logoBytes = null);
}

public interface IFacturacionService
{
    Task<BillingSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<FacturacionOperationResult> UpdateSettingsAsync(UpdateBillingSettingsCommand command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaxDefinitionDto>> GetTaxesAsync(bool includeInactive = true, CancellationToken cancellationToken = default);
    Task<FacturacionOperationResult> CreateTaxAsync(CreateTaxRateCommand command, CancellationToken cancellationToken = default);
    Task<FacturacionOperationResult> UpdateTaxAsync(UpdateTaxRateCommand command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillingSeriesDto>> GetSeriesAsync(int? fiscalYear = null, CancellationToken cancellationToken = default);
    Task<BillingSeriesDto> EnsureDefaultSeriesForYearAsync(BillingDocumentKind kind, int fiscalYear, CancellationToken cancellationToken = default);
    Task<FacturacionOperationResult> SaveSeriesAsync(SaveBillingSeriesCommand command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuoteDto>> GetQuotesAsync(CancellationToken cancellationToken = default);
    Task<QuoteDto?> GetQuoteByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FacturacionOperationResult> CreateQuoteAsync(SaveQuoteCommand command, CancellationToken cancellationToken = default);
    Task<FacturacionOperationResult> UpdateQuoteAsync(SaveQuoteCommand command, CancellationToken cancellationToken = default);
    Task<FacturacionOperationResult> ChangeQuoteStatusAsync(Guid quoteId, QuoteStatus newStatus, uint rowVersion, CancellationToken cancellationToken = default);
    Task<FacturacionOperationResult> ConvertQuoteToDraftInvoiceAsync(Guid quoteId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvoiceDto>> GetInvoicesAsync(CancellationToken cancellationToken = default);
    Task<InvoiceDto?> GetInvoiceByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FacturacionOperationResult> SaveDraftInvoiceAsync(SaveDraftInvoiceCommand command, CancellationToken cancellationToken = default);
    Task<FacturacionOperationResult> DeleteDraftInvoiceAsync(Guid draftInvoiceId, CancellationToken cancellationToken = default);
    Task<FacturacionOperationResult> IssueDraftInvoiceAsync(Guid draftInvoiceId, uint rowVersion, CancellationToken cancellationToken = default);
    Task<FacturacionOperationResult> IssueRectifyingInvoiceAsync(IssueRectifyingInvoiceRequest request, CancellationToken cancellationToken = default);

    Task<PdfDocumentResult> DownloadInvoicePdfAsync(Guid invoiceId, CancellationToken cancellationToken = default);
    Task<PdfDocumentResult> DownloadQuotePdfAsync(Guid quoteId, CancellationToken cancellationToken = default);
}
