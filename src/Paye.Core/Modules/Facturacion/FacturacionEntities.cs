using System.Globalization;
using Paye.Core.Entities;
using Paye.Core.Invoicing;

namespace Paye.Core.Modules.Facturacion;

public enum BillingDocumentKind
{
    Presupuesto = 1,
    Ordinaria = 2,
    Simplificada = 3,
    Rectificativa = 4
}

public enum QuoteStatus
{
    Borrador = 1,
    Enviado = 2,
    Aceptado = 3,
    Rechazado = 4,
    Caducado = 5
}

public enum InvoiceStatus
{
    Borrador = 1,
    Emitida = 2
}

/// <summary>
/// Configuración fiscal y de plantilla del emisor editable desde la UI.
/// Solo bloquea la acción de "Emitir factura" si faltan o son inválidos TaxName, TaxId o FiscalAddress.
/// </summary>
public sealed class BillingSettings : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TaxName { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string FiscalAddress { get; set; } = string.Empty;
    public Guid? LogoPrivateFileId { get; set; }
    public string LegalTermsText { get; set; } = string.Empty;
    public string FooterText { get; set; } = string.Empty;
}

/// <summary>
/// Catálogo configurable de impuestos (IVA, Recargo de Equivalencia ligado a un IVA, e IRPF).
/// No admite borrado físico ni lógico: solo creación, edición de nombre/aplicabilidad/activo y desactivación.
/// </summary>
public sealed class TaxRateDefinition : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TaxKind Kind { get; set; } = TaxKind.Iva;
    public decimal Percentage { get; set; }
    public Guid? ParentVatTaxId { get; set; }
    public TaxApplicability Applicability { get; set; } = TaxApplicability.Ambos;
    public string AeatTaxCode { get; set; } = "01";
    public string AeatRegimeKey { get; set; } = "01";
    public string AeatOperationQualification { get; set; } = "S1";
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
}

/// <summary>
/// Serie de numeración correlativa por tipo de documento y ejercicio fiscal (hora de Madrid).
/// </summary>
public sealed class BillingSeries : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public BillingDocumentKind DocumentKind { get; set; }
    public int FiscalYear { get; set; }
    public int LastNumber { get; set; }
    public int PaddingDigits { get; set; } = 4;
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }

    public string FormatDocumentNumber(int sequentialNumber) =>
        $"{Code}-{FiscalYear.ToString(CultureInfo.InvariantCulture)}-{sequentialNumber.ToString(CultureInfo.InvariantCulture).PadLeft(Math.Max(1, PaddingDigits), '0')}";
}

/// <summary>
/// Presupuesto comercial. Se numera al crearse en su propia serie no fiscal (DocumentKind = Presupuesto)
/// y puede convertirse en borrador de factura cuando está en estado Aceptado.
/// </summary>
public sealed class Quote : AuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SeriesId { get; set; }
    public string SeriesCode { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public int SequentialNumber { get; set; }
    public string FormattedNumber { get; set; } = string.Empty;
    public QuoteStatus Status { get; set; } = QuoteStatus.Borrador;
    public DateOnly IssueDate { get; set; }
    public DateOnly? ValidUntilDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerTaxId { get; set; }
    public string? CustomerAddress { get; set; }

    public Guid? WithholdingTaxId { get; set; }
    public string? WithholdingTaxName { get; set; }
    public decimal WithholdingPercentage { get; set; }

    public decimal SubtotalNet { get; set; }
    public decimal TotalVat { get; set; }
    public decimal TotalSurcharge { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalWithholding { get; set; }
    public decimal TotalPayable { get; set; }

    public string? Notes { get; set; }
    public Guid? ConvertedInvoiceId { get; set; }

    public List<QuoteLine> Lines { get; set; } = [];

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}

/// <summary>
/// Línea de presupuesto. Persiste LineNet (base imponible redondeada de la línea) y el snapshot de IVA y R.E.;
/// las cuotas se calculan de forma agregada por tipo impositivo en InvoiceTotalsCalculator.
/// </summary>
public sealed class QuoteLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuoteId { get; set; }
    public int LineOrder { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPriceNet { get; set; }

    public Guid? VatTaxId { get; set; }
    public string VatTaxName { get; set; } = "IVA 21%";
    public decimal VatPercentage { get; set; }

    public Guid? SurchargeTaxId { get; set; }
    public string? SurchargeTaxName { get; set; }
    public decimal SurchargePercentage { get; set; }

    public decimal LineNet { get; set; }
}

/// <summary>
/// Factura (Borrador o Emitida; Ordinaria, Simplificada o Rectificativa).
/// NO implementa ISoftDeletable: los borradores pueden eliminarse físicamente antes de emitir,
/// y las facturas emitidas están protegidas por triggers de inmutabilidad en PostgreSQL.
/// </summary>
public sealed class Invoice : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public InvoiceDocumentType InvoiceType { get; set; } = InvoiceDocumentType.Ordinaria;
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Borrador;
    public Guid SeriesId { get; set; }
    public string SeriesCode { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public int? SequentialNumber { get; set; }
    public string? FormattedNumber { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateTimeOffset? IssuedAtUtc { get; set; }

    public string IssuerTaxName { get; set; } = string.Empty;
    public string IssuerTaxId { get; set; } = string.Empty;
    public string IssuerFiscalAddress { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerTaxId { get; set; }
    public string? CustomerAddress { get; set; }
    public BillingPaymentMethod PaymentMethod { get; set; } = BillingPaymentMethod.Contado;

    public Guid? WithholdingTaxId { get; set; }
    public string? WithholdingTaxName { get; set; }
    public decimal WithholdingPercentage { get; set; }

    public decimal SubtotalNet { get; set; }
    public decimal TotalVat { get; set; }
    public decimal TotalSurcharge { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalWithholding { get; set; }
    public decimal TotalPayable { get; set; }

    public string? Notes { get; set; }

    public Guid? SourceQuoteId { get; set; }
    public Guid? RectifiedInvoiceId { get; set; }
    public RectificationScope? RectificationScope { get; set; }
    public string? RectificationReason { get; set; }
    public string? FiscalRegistryMetadata { get; set; }

    public List<InvoiceLine> Lines { get; set; } = [];
    public List<InvoiceTaxBreakdown> TaxBreakdowns { get; set; } = [];
}

/// <summary>
/// Línea de factura. Persiste LineNet (base imponible de la línea) y el snapshot de IVA y R.E.
/// No persiste LineTax para que el total siempre cuadre al céntimo con el cuadro de desglose por tipo.
/// </summary>
public sealed class InvoiceLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public int LineOrder { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPriceNet { get; set; }

    public Guid? VatTaxId { get; set; }
    public string VatTaxName { get; set; } = "IVA 21%";
    public decimal VatPercentage { get; set; }

    public Guid? SurchargeTaxId { get; set; }
    public string? SurchargeTaxName { get; set; }
    public decimal SurchargePercentage { get; set; }

    public decimal LineNet { get; set; }
}

/// <summary>
/// Snapshot inmutable de cada impuesto aplicado a una factura (IVA, Recargo de Equivalencia e IRPF),
/// con sus claves AEAT (Impuesto, ClaveRegimen, CalificacionOperacion), base imponible y cuota.
/// Protegida por trigger PostgreSQL trg_facturacion_factura_impuestos_immutable cuando la factura está Emitida.
/// </summary>
public sealed class InvoiceTaxBreakdown
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public Guid? TaxId { get; set; }
    public TaxKind TaxKind { get; set; }
    public string TaxName { get; set; } = string.Empty;
    public decimal TaxPercentage { get; set; }
    public decimal? LinkedVatPercentage { get; set; }
    public string AeatTaxCode { get; set; } = "01";
    public string AeatRegimeKey { get; set; } = "01";
    public string AeatOperationQualification { get; set; } = "S1";
    public decimal TaxableBase { get; set; }
    public decimal TaxAmount { get; set; }
}
