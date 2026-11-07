using System.ComponentModel.DataAnnotations;
using Paye.Core.Invoicing;
using Paye.Core.Modules.Facturacion;

namespace Paye.Web.Modules.Facturacion;

public sealed class FacturacionIndexViewModel
{
    public required IReadOnlyList<InvoiceDto> Invoices { get; init; }
    public required IReadOnlyList<QuoteDto> Quotes { get; init; }
    public required BillingSettingsDto Settings { get; init; }
    public string ActiveTab { get; init; } = "facturas";
    public bool CanCreate { get; init; }
    public bool CanIssue { get; init; }
    public bool CanRectify { get; init; }
    public bool CanDownload { get; init; }
    public bool CanConfigure { get; init; }
}

public sealed class DocumentLineFormInputModel
{
    [StringLength(300, ErrorMessage = "La descripción no puede superar los 300 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0,0001", "999999999", ConvertValueInInvariantCulture = false)]
    public decimal Quantity { get; set; } = 1m;

    public decimal UnitPriceNet { get; set; }

    public Guid? VatTaxId { get; set; }

    public decimal VatPercentage { get; set; } = 21m;

    public Guid? SurchargeTaxId { get; set; }

    public decimal SurchargePercentage { get; set; }
}

public sealed class QuoteFormViewModel
{
    public Guid? Id { get; set; }

    public Guid? SeriesId { get; set; }

    public DateOnly? IssueDate { get; set; }

    public DateOnly? ValidUntilDate { get; set; }

    [Required(ErrorMessage = "El nombre del cliente es obligatorio.")]
    [StringLength(200, ErrorMessage = "El nombre del cliente no puede superar los 200 caracteres.")]
    public string CustomerName { get; set; } = string.Empty;

    [StringLength(32, ErrorMessage = "El NIF/CIF/NIE no puede superar los 32 caracteres.")]
    public string? CustomerTaxId { get; set; }

    [StringLength(500, ErrorMessage = "La dirección del cliente no puede superar los 500 caracteres.")]
    public string? CustomerAddress { get; set; }

    public Guid? WithholdingTaxId { get; set; }

    public decimal WithholdingPercentage { get; set; }

    [StringLength(2000, ErrorMessage = "Las observaciones no pueden superar los 2000 caracteres.")]
    public string? Notes { get; set; }

    public uint? RowVersion { get; set; }

    public List<DocumentLineFormInputModel> Lines { get; set; } = [];

    public IReadOnlyList<BillingSeriesDto> AvailableSeries { get; set; } = [];
    public IReadOnlyList<TaxDefinitionDto> AvailableVatTaxes { get; set; } = [];
    public IReadOnlyList<TaxDefinitionDto> AvailableSurchargeTaxes { get; set; } = [];
    public IReadOnlyList<TaxDefinitionDto> AvailableWithholdingTaxes { get; set; } = [];
}

public sealed class QuoteDetailsViewModel
{
    public required QuoteDto Quote { get; init; }
    public bool CanCreate { get; init; }
    public bool CanDownload { get; init; }
}

public sealed class ChangeQuoteStatusInputModel
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public QuoteStatus NewStatus { get; set; }

    [Required]
    public uint RowVersion { get; set; }
}

public sealed class InvoiceFormViewModel
{
    public Guid? Id { get; set; }

    public InvoiceDocumentType InvoiceType { get; set; } = InvoiceDocumentType.Ordinaria;

    public Guid? SeriesId { get; set; }

    public DateOnly? IssueDate { get; set; }

    [StringLength(200, ErrorMessage = "El nombre del cliente no puede superar los 200 caracteres.")]
    public string CustomerName { get; set; } = string.Empty;

    [StringLength(32, ErrorMessage = "El NIF/CIF/NIE no puede superar los 32 caracteres.")]
    public string? CustomerTaxId { get; set; }

    [StringLength(500, ErrorMessage = "La dirección del cliente no puede superar los 500 caracteres.")]
    public string? CustomerAddress { get; set; }

    public BillingPaymentMethod PaymentMethod { get; set; } = BillingPaymentMethod.Contado;

    public Guid? WithholdingTaxId { get; set; }

    public decimal WithholdingPercentage { get; set; }

    [StringLength(2000, ErrorMessage = "Las observaciones no pueden superar los 2000 caracteres.")]
    public string? Notes { get; set; }

    public uint? RowVersion { get; set; }

    public List<DocumentLineFormInputModel> Lines { get; set; } = [];

    public IReadOnlyList<BillingSeriesDto> AvailableSeries { get; set; } = [];
    public IReadOnlyList<TaxDefinitionDto> AvailableVatTaxes { get; set; } = [];
    public IReadOnlyList<TaxDefinitionDto> AvailableSurchargeTaxes { get; set; } = [];
    public IReadOnlyList<TaxDefinitionDto> AvailableWithholdingTaxes { get; set; } = [];
}

public sealed class InvoiceDetailsViewModel
{
    public required InvoiceDto Invoice { get; init; }
    public required BillingSettingsDto Settings { get; init; }
    public bool CanCreate { get; init; }
    public bool CanIssue { get; init; }
    public bool CanRectify { get; init; }
    public bool CanDownload { get; init; }
}

public sealed class IssueDraftInvoiceInputModel
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public uint RowVersion { get; set; }
}

public sealed class RectifyInvoiceFormViewModel
{
    [Required]
    public Guid OriginalInvoiceId { get; set; }

    public string OriginalFormattedNumber { get; set; } = string.Empty;

    public string OriginalCustomerName { get; set; } = string.Empty;

    public decimal OriginalTotalGross { get; set; }

    public Guid? SeriesId { get; set; }

    [Required]
    public RectificationScope Scope { get; set; } = RectificationScope.Total;

    [Required(ErrorMessage = "El motivo de rectificación es obligatorio.")]
    [StringLength(500, ErrorMessage = "El motivo de rectificación no puede superar los 500 caracteres.")]
    public string Reason { get; set; } = string.Empty;

    public BillingPaymentMethod PaymentMethod { get; set; } = BillingPaymentMethod.Contado;

    [StringLength(2000)]
    public string? Notes { get; set; }

    public List<DocumentLineFormInputModel> DifferenceLines { get; set; } = [];

    public IReadOnlyList<BillingSeriesDto> AvailableSeries { get; set; } = [];
    public IReadOnlyList<TaxDefinitionDto> AvailableVatTaxes { get; set; } = [];
    public IReadOnlyList<TaxDefinitionDto> AvailableSurchargeTaxes { get; set; } = [];
}

public sealed class FacturacionSettingsViewModel
{
    public required BillingSettingsInputModel IssuerInput { get; init; }
    public required SaveBillingSeriesInputModel NewSeriesInput { get; init; }
    public required CreateTaxRateInputModel NewTaxInput { get; init; }
    public required IReadOnlyList<BillingSeriesDto> Series { get; init; }
    public required IReadOnlyList<TaxDefinitionDto> Taxes { get; init; }
}

public sealed class BillingSettingsInputModel
{
    [Required(ErrorMessage = "La razón social o nombre fiscal del emisor es obligatorio.")]
    [StringLength(200, ErrorMessage = "La razón social no puede superar los 200 caracteres.")]
    public string TaxName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El NIF/CIF/NIE del emisor es obligatorio.")]
    [StringLength(32, ErrorMessage = "El NIF/CIF/NIE no puede superar los 32 caracteres.")]
    public string TaxId { get; set; } = string.Empty;

    [Required(ErrorMessage = "El domicilio fiscal del emisor es obligatorio.")]
    [StringLength(500, ErrorMessage = "El domicilio fiscal no puede superar los 500 caracteres.")]
    public string FiscalAddress { get; set; } = string.Empty;

    public Guid? LogoPrivateFileId { get; set; }

    [StringLength(2000, ErrorMessage = "Los textos legales no pueden superar los 2000 caracteres.")]
    public string LegalTermsText { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "El pie de factura no puede superar los 500 caracteres.")]
    public string FooterText { get; set; } = string.Empty;

    [Required]
    public uint RowVersion { get; set; }
}

public sealed class SaveBillingSeriesInputModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "El código de serie es obligatorio.")]
    [StringLength(16, ErrorMessage = "El código de serie no puede superar los 16 caracteres.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre de serie es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre de serie no puede superar los 120 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required]
    public BillingDocumentKind DocumentKind { get; set; } = BillingDocumentKind.Ordinaria;

    [Range(2000, 2100, ErrorMessage = "El ejercicio fiscal debe estar entre 2000 y 2100.")]
    public int FiscalYear { get; set; } = DateTime.UtcNow.Year;

    [Range(1, 10, ErrorMessage = "Los dígitos de relleno deben estar entre 1 y 10.")]
    public int PaddingDigits { get; set; } = 5;

    public bool IsActive { get; set; } = true;

    public bool IsDefault { get; set; }
}

public sealed class CreateTaxRateInputModel
{
    [Required(ErrorMessage = "El código del impuesto es obligatorio.")]
    [StringLength(32, ErrorMessage = "El código no puede superar los 32 caracteres.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre del impuesto es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar los 120 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required]
    public TaxKind Kind { get; set; } = TaxKind.Iva;

    [Range(typeof(decimal), "0", "100")]
    public decimal Percentage { get; set; }

    public Guid? ParentVatTaxId { get; set; }

    [Required]
    public TaxApplicability Applicability { get; set; } = TaxApplicability.Ambos;

    public bool IsDefault { get; set; }
}

public sealed class UpdateTaxRateInputModel
{
    [Required]
    public Guid Id { get; set; }

    [Required(ErrorMessage = "El nombre del impuesto es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar los 120 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required]
    public TaxApplicability Applicability { get; set; } = TaxApplicability.Ambos;

    public bool IsActive { get; set; }

    public bool IsDefault { get; set; }

    [Required]
    public uint RowVersion { get; set; }
}
