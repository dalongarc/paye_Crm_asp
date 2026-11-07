using Paye.Core.Invoicing;

namespace Paye.Core.Modules.Facturacion;

public sealed record FiscalTaxBreakdownEntry(
    TaxKind TaxKind,
    string TaxName,
    decimal TaxPercentage,
    decimal? LinkedVatPercentage,
    string AeatTaxCode,
    string AeatRegimeKey,
    string AeatOperationQualification,
    decimal TaxableBase,
    decimal TaxAmount);

public sealed record IssuedInvoiceFiscalContext(
    Guid InvoiceId,
    InvoiceDocumentType InvoiceType,
    string SeriesCode,
    int FiscalYear,
    int SequentialNumber,
    string FormattedNumber,
    DateTimeOffset IssuedAtUtc,
    DateOnly IssueDate,
    string IssuerTaxId,
    string IssuerTaxName,
    string CustomerName,
    string? CustomerTaxId,
    decimal SubtotalNet,
    decimal TotalVat,
    decimal TotalSurcharge,
    decimal TotalTax,
    decimal TotalGross,
    decimal TotalWithholding,
    decimal TotalPayable,
    Guid? RectifiedInvoiceId,
    string? RectifiedFormattedNumber,
    IReadOnlyList<FiscalTaxBreakdownEntry> TaxBreakdown);

public sealed record FiscalRegistrationResult(
    bool Succeeded,
    string? RegistryMetadata = null,
    string? ErrorMessage = null)
{
    public static FiscalRegistrationResult Ok(string? metadata = null) =>
        new(true, metadata, null);

    public static FiscalRegistrationResult Fail(string errorMessage) =>
        new(false, null, errorMessage);
}

/// <summary>
/// Punto de extensión desacoplado para el sistema de emisión de facturas verificables (VERI*FACTU / NO VERI*FACTU).
/// Se invoca dentro de la transacción de emisión antes del COMMIT. Si devuelve fallo o lanza excepción,
/// la transacción completa hace ROLLBACK sin dejar huecos en la numeración fiscal.
/// </summary>
public interface IFiscalInvoiceRegistry
{
    Task<FiscalRegistrationResult> RegisterIssuedInvoiceAsync(
        IssuedInvoiceFiscalContext context,
        CancellationToken cancellationToken = default);
}
