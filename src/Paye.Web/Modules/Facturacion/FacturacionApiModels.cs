using System.ComponentModel.DataAnnotations;
using Paye.Core.Invoicing;

namespace Paye.Web.Modules.Facturacion;

public sealed record CreateQuoteJsonRequest(
    Guid? SeriesId,
    DateOnly IssueDate,
    DateOnly? ValidUntilDate,
    [Required] string CustomerName,
    string? CustomerTaxId,
    string? CustomerAddress,
    string? Notes,
    List<DocumentLineJsonDto>? Lines,
    Guid? WithholdingTaxId,
    decimal? WithholdingPercentage);

public sealed record CreateInvoiceJsonRequest(
    InvoiceDocumentType InvoiceType,
    Guid? SeriesId,
    DateOnly IssueDate,
    [Required] string CustomerName,
    string? CustomerTaxId,
    string? CustomerAddress,
    BillingPaymentMethod PaymentMethod,
    string? Notes,
    List<DocumentLineJsonDto>? Lines,
    Guid? WithholdingTaxId,
    decimal? WithholdingPercentage);

public sealed record DocumentLineJsonDto(
    string Description,
    decimal Quantity,
    decimal UnitPriceNet,
    decimal VatPercentage,
    Guid? VatTaxId,
    Guid? SurchargeTaxId,
    decimal? SurchargePercentage);

public sealed record IssueInvoiceJsonRequest(
    uint RowVersion);

public sealed record RectifyInvoiceJsonRequest(
    Guid? SeriesId,
    DateOnly IssueDate,
    RectificationScope Scope,
    string RectificationReason,
    BillingPaymentMethod PaymentMethod,
    List<DocumentLineJsonDto>? DifferenceLines,
    string? Notes);
