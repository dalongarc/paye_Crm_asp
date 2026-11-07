namespace Paye.Core.Invoicing;

/// <summary>
/// Tipo de factura contemplado en el modelo fiscal (incluye Simplificada para el futuro módulo Pos).
/// </summary>
public enum InvoiceDocumentType
{
    Ordinaria = 1,
    Simplificada = 2,
    Rectificativa = 3
}

/// <summary>
/// Método de pago registrado a efectos informativos y contables (sin pasarela externa).
/// </summary>
public enum BillingPaymentMethod
{
    Contado = 1,
    Tarjeta = 2,
    Bizum = 3
}

/// <summary>
/// Alcance de una factura rectificativa: anulación total (100 % inverso) o ajuste parcial por diferencias.
/// </summary>
public enum RectificationScope
{
    Total = 1,
    PorDiferencias = 2
}

/// <summary>
/// Naturaleza del impuesto en el catálogo configurable.
/// </summary>
public enum TaxKind
{
    Iva = 1,
    RecargoEquivalencia = 2,
    Irpf = 3
}

/// <summary>
/// Ámbito de aplicación permitido para cada impuesto del catálogo.
/// </summary>
public enum TaxApplicability
{
    Ambos = 1,
    B2B = 2,
    B2C = 3
}

public sealed record TaxDefinitionDto(
    Guid Id,
    string Code,
    string Name,
    TaxKind Kind,
    decimal Percentage,
    Guid? ParentVatTaxId,
    decimal? ParentVatPercentage,
    TaxApplicability Applicability,
    string AeatTaxCode,
    string AeatRegimeKey,
    string AeatOperationQualification,
    bool IsActive,
    bool IsDefault,
    uint RowVersion);

/// <summary>
/// Contrato de solo lectura en Paye.Core para que el futuro módulo Pos consulte el catálogo de impuestos activos
/// sin depender de Paye.Core.Modules.Facturacion.
/// </summary>
public interface ITaxCatalogReader
{
    Task<IReadOnlyList<TaxDefinitionDto>> GetActiveTaxesAsync(
        TaxApplicability? applicabilityFilter = null,
        CancellationToken cancellationToken = default);
}

public sealed record IssueInvoiceLineRequest(
    string Description,
    decimal Quantity,
    decimal UnitPriceNet,
    decimal VatPercentage,
    Guid? VatTaxId = null,
    Guid? SurchargeTaxId = null,
    decimal SurchargePercentage = 0m);

public sealed record IssueInvoiceRequest(
    InvoiceDocumentType InvoiceType,
    BillingPaymentMethod PaymentMethod,
    string CustomerName,
    string? CustomerTaxId,
    string? CustomerAddress,
    IReadOnlyList<IssueInvoiceLineRequest> Lines,
    Guid? SeriesId = null,
    DateOnly? IssueDate = null,
    string? Notes = null,
    Guid? WithholdingTaxId = null,
    decimal WithholdingPercentage = 0m);

public sealed record IssueRectifyingInvoiceRequest(
    Guid OriginalInvoiceId,
    RectificationScope Scope,
    string Reason,
    BillingPaymentMethod PaymentMethod,
    IReadOnlyList<IssueInvoiceLineRequest>? DifferenceLines = null,
    Guid? SeriesId = null);

public sealed record IssuedInvoiceSummary(
    Guid Id,
    InvoiceDocumentType InvoiceType,
    string SeriesCode,
    int FiscalYear,
    int SequentialNumber,
    string FormattedNumber,
    DateTimeOffset IssuedAtUtc,
    decimal SubtotalNet,
    decimal TotalVat,
    decimal TotalSurcharge,
    decimal TotalTax,
    decimal TotalGross,
    decimal TotalWithholding,
    decimal TotalPayable);

public sealed record InvoiceEmissionResult(
    bool Succeeded,
    string? ErrorMessage = null,
    IssuedInvoiceSummary? Invoice = null)
{
    public static InvoiceEmissionResult Success(IssuedInvoiceSummary invoice) =>
        new(true, null, invoice);

    public static InvoiceEmissionResult Failure(string errorMessage) =>
        new(false, errorMessage, null);
}

/// <summary>
/// Contrato compartido en Paye.Core para emisión de facturas (ordinarias, simplificadas/tickets y rectificativas)
/// que podrá consumir el futuro módulo Pos sin acoplar Facturacion con Pos.
/// </summary>
public interface IInvoiceIssuer
{
    Task<InvoiceEmissionResult> CreateAndIssueAsync(
        IssueInvoiceRequest request,
        CancellationToken cancellationToken = default);

    Task<InvoiceEmissionResult> IssueRectifyingAsync(
        IssueRectifyingInvoiceRequest request,
        CancellationToken cancellationToken = default);
}
