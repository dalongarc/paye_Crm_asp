using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Paye.Core.Invoicing;
using Paye.Core.Modules.Facturacion;
using Paye.Infrastructure.Modules.Facturacion;

namespace Paye.Web.Modules.Facturacion;

[Authorize(Policy = FacturacionPermissions.Ver)]
public sealed class FacturacionController(
    IFacturacionService facturacionService,
    IAuthorizationService authorizationService) : Controller
{
    [HttpGet("/facturacion")]
    public async Task<IActionResult> Index(
        [FromQuery] string? tab = null,
        CancellationToken cancellationToken = default)
    {
        var invoices = await facturacionService.GetInvoicesAsync(cancellationToken);
        var quotes = await facturacionService.GetQuotesAsync(cancellationToken);
        var settings = await facturacionService.GetSettingsAsync(cancellationToken);

        var canCreate = (await authorizationService.AuthorizeAsync(User, FacturacionPermissions.Crear)).Succeeded;
        var canIssue = (await authorizationService.AuthorizeAsync(User, FacturacionPermissions.Emitir)).Succeeded;
        var canRectify = (await authorizationService.AuthorizeAsync(User, FacturacionPermissions.Rectificar)).Succeeded;
        var canDownload = (await authorizationService.AuthorizeAsync(User, FacturacionPermissions.Descargar)).Succeeded;
        var canConfigure = (await authorizationService.AuthorizeAsync(User, FacturacionPermissions.Configurar)).Succeeded;

        var activeTab = string.Equals(tab, "presupuestos", StringComparison.OrdinalIgnoreCase)
            ? "presupuestos"
            : "facturas";

        var vm = new FacturacionIndexViewModel
        {
            Invoices = invoices,
            Quotes = quotes,
            Settings = settings,
            ActiveTab = activeTab,
            CanCreate = canCreate,
            CanIssue = canIssue,
            CanRectify = canRectify,
            CanDownload = canDownload,
            CanConfigure = canConfigure
        };

        return View(vm);
    }

    [HttpGet("/facturacion/api/invoices")]
    [Authorize(Policy = FacturacionPermissions.Ver)]
    public async Task<IActionResult> GetInvoicesJson(CancellationToken cancellationToken = default)
    {
        var invoices = await facturacionService.GetInvoicesAsync(cancellationToken);
        var settings = await facturacionService.GetSettingsAsync(cancellationToken);

        var canCreate = (await authorizationService.AuthorizeAsync(User, FacturacionPermissions.Crear)).Succeeded;
        var canIssue = (await authorizationService.AuthorizeAsync(User, FacturacionPermissions.Emitir)).Succeeded;
        var canRectify = (await authorizationService.AuthorizeAsync(User, FacturacionPermissions.Rectificar)).Succeeded;
        var canDownload = (await authorizationService.AuthorizeAsync(User, FacturacionPermissions.Descargar)).Succeeded;
        var canConfigure = (await authorizationService.AuthorizeAsync(User, FacturacionPermissions.Configurar)).Succeeded;

        var issuedInvoices = invoices.Where(i => i.Status == InvoiceStatus.Emitida).ToList();
        var draftInvoices = invoices.Where(i => i.Status == InvoiceStatus.Borrador).ToList();
        var rectifiedInvoices = invoices
            .Where(i => i.InvoiceType == InvoiceDocumentType.Rectificativa)
            .ToList();

        var response = new
        {
            summary = new
            {
                totalInvoicesCount = invoices.Count,
                issuedCount = issuedInvoices.Count,
                draftCount = draftInvoices.Count,
                rectifiedCount = rectifiedInvoices.Count,
                totalTaxableBase = issuedInvoices.Sum(i => i.SubtotalNet),
                totalTaxes = issuedInvoices.Sum(i => i.TotalTax),
                totalWithholding = issuedInvoices.Sum(i => i.TotalWithholding),
                totalBilledPayable = issuedInvoices.Sum(i => i.TotalPayable),
                draftPendingAmount = draftInvoices.Sum(i => i.TotalPayable)
            },
            issuer = new
            {
                taxName = settings.TaxName,
                taxId = settings.TaxId,
                fiscalAddress = settings.FiscalAddress,
                isReadyToIssue = settings.IsReadyToIssue
            },
            permissions = new
            {
                canCreate,
                canIssue,
                canRectify,
                canDownload,
                canConfigure
            },
            items = invoices.Select(i => new
            {
                id = i.Id,
                formattedNumber = i.FormattedNumber,
                seriesCode = i.SeriesCode,
                fiscalYear = i.FiscalYear,
                invoiceType = i.InvoiceType.ToString(),
                status = i.Status.ToString(),
                issueDate = i.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                issuedAtUtc = i.IssuedAtUtc?.ToString("O", CultureInfo.InvariantCulture),
                customerName = i.CustomerName,
                customerTaxId = i.CustomerTaxId,
                paymentMethod = i.PaymentMethod.ToString(),
                subtotalNet = i.SubtotalNet,
                totalVat = i.TotalVat,
                totalSurcharge = i.TotalSurcharge,
                totalWithholding = i.TotalWithholding,
                totalGross = i.TotalGross,
                totalPayable = i.TotalPayable,
                rectifiedInvoiceId = i.RectifiedInvoiceId,
                linesCount = i.Lines.Count
            })
        };

        return Json(response);
    }

    [HttpGet("/facturacion/api/invoices/{id:guid}")]
    [Authorize(Policy = FacturacionPermissions.Ver)]
    public async Task<IActionResult> GetInvoiceDetailJson(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await facturacionService.GetInvoiceByIdAsync(id, cancellationToken);
        if (invoice is null)
        {
            return NotFound(new { message = "La factura solicitada no existe." });
        }

        var response = new
        {
            id = invoice.Id,
            formattedNumber = invoice.FormattedNumber,
            seriesCode = invoice.SeriesCode,
            fiscalYear = invoice.FiscalYear,
            sequentialNumber = invoice.SequentialNumber,
            invoiceType = invoice.InvoiceType.ToString(),
            status = invoice.Status.ToString(),
            issueDate = invoice.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            issuedAtUtc = invoice.IssuedAtUtc?.ToString("O", CultureInfo.InvariantCulture),
            issuerTaxName = invoice.IssuerTaxName,
            issuerTaxId = invoice.IssuerTaxId,
            issuerFiscalAddress = invoice.IssuerFiscalAddress,
            customerName = invoice.CustomerName,
            customerTaxId = invoice.CustomerTaxId,
            customerAddress = invoice.CustomerAddress,
            paymentMethod = invoice.PaymentMethod.ToString(),
            withholdingTaxName = invoice.WithholdingTaxName,
            withholdingPercentage = invoice.WithholdingPercentage,
            subtotalNet = invoice.SubtotalNet,
            totalVat = invoice.TotalVat,
            totalSurcharge = invoice.TotalSurcharge,
            totalTax = invoice.TotalTax,
            totalGross = invoice.TotalGross,
            totalWithholding = invoice.TotalWithholding,
            totalPayable = invoice.TotalPayable,
            notes = invoice.Notes,
            sourceQuoteId = invoice.SourceQuoteId,
            rectifiedInvoiceId = invoice.RectifiedInvoiceId,
            rectificationScope = invoice.RectificationScope?.ToString(),
            rectificationReason = invoice.RectificationReason,
            rowVersion = invoice.RowVersion,
            lines = invoice.Lines.Select(l => new
            {
                id = l.Id,
                lineOrder = l.LineOrder,
                description = l.Description,
                quantity = l.Quantity,
                unitPriceNet = l.UnitPriceNet,
                vatTaxName = l.VatTaxName,
                vatPercentage = l.VatPercentage,
                surchargeTaxName = l.SurchargeTaxName,
                surchargePercentage = l.SurchargePercentage,
                lineNet = l.LineNet
            }),
            vatBreakdown = invoice.VatBreakdown.Select(vb => new
            {
                vatPercentage = vb.VatPercentage,
                vatTaxName = vb.VatTaxName,
                taxableBase = vb.TaxableBase,
                taxAmount = vb.TaxAmount,
                surchargePercentage = vb.SurchargePercentage,
                surchargeTaxName = vb.SurchargeTaxName,
                surchargeAmount = vb.SurchargeAmount,
                grossAmount = vb.GrossAmount
            }),
            appliedTaxes = invoice.AppliedTaxes.Select(at => new
            {
                id = at.Id,
                taxKind = at.TaxKind.ToString(),
                taxName = at.TaxName,
                taxPercentage = at.TaxPercentage,
                linkedVatPercentage = at.LinkedVatPercentage,
                aeatTaxCode = at.AeatTaxCode,
                aeatRegimeKey = at.AeatRegimeKey,
                aeatOperationQualification = at.AeatOperationQualification,
                taxableBase = at.TaxableBase,
                taxAmount = at.TaxAmount
            })
        };

        return Json(response);
    }

    [HttpGet("/facturacion/api/quotes")]
    [Authorize(Policy = FacturacionPermissions.Ver)]
    public async Task<IActionResult> GetQuotesJson(CancellationToken cancellationToken = default)
    {
        var quotes = await facturacionService.GetQuotesAsync(cancellationToken);
        return Json(new
        {
            items = quotes.Select(q => new
            {
                id = q.Id,
                formattedNumber = q.FormattedNumber,
                seriesCode = q.SeriesCode,
                fiscalYear = q.FiscalYear,
                status = q.Status.ToString(),
                issueDate = q.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                validUntilDate = q.ValidUntilDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                customerName = q.CustomerName,
                customerTaxId = q.CustomerTaxId,
                subtotalNet = q.SubtotalNet,
                totalVat = q.TotalVat,
                totalPayable = q.TotalPayable,
                linesCount = q.Lines.Count
            })
        });
    }

    [HttpGet("/facturacion/api/catalogs")]
    [Authorize(Policy = FacturacionPermissions.Ver)]
    public async Task<IActionResult> GetCatalogsJson(CancellationToken cancellationToken = default)
    {
        var currentYear = FiscalTimeZone.GetMadridFiscalYear(DateTimeOffset.UtcNow);
        await facturacionService.EnsureDefaultSeriesForYearAsync(BillingDocumentKind.Presupuesto, currentYear, cancellationToken);
        await facturacionService.EnsureDefaultSeriesForYearAsync(BillingDocumentKind.Ordinaria, currentYear, cancellationToken);
        await facturacionService.EnsureDefaultSeriesForYearAsync(BillingDocumentKind.Rectificativa, currentYear, cancellationToken);

        var allSeries = await facturacionService.GetSeriesAsync(null, cancellationToken);
        var activeTaxes = await facturacionService.GetTaxesAsync(includeInactive: false, cancellationToken);

        return Json(new
        {
            series = allSeries.Where(s => s.IsActive).Select(s => new
            {
                id = s.Id,
                code = s.Code,
                name = s.Name,
                documentKind = s.DocumentKind.ToString(),
                fiscalYear = s.FiscalYear,
                isDefault = s.IsDefault
            }),
            vatTaxes = activeTaxes.Where(t => t.Kind == TaxKind.Iva).Select(t => new
            {
                id = t.Id,
                name = t.Name,
                percentage = t.Percentage,
                isDefault = t.IsDefault
            }),
            surchargeTaxes = activeTaxes.Where(t => t.Kind == TaxKind.RecargoEquivalencia).Select(t => new
            {
                id = t.Id,
                name = t.Name,
                percentage = t.Percentage
            }),
            withholdingTaxes = activeTaxes.Where(t => t.Kind == TaxKind.Irpf).Select(t => new
            {
                id = t.Id,
                name = t.Name,
                percentage = t.Percentage
            }),
            paymentMethods = Enum.GetNames<BillingPaymentMethod>()
        });
    }

    [HttpPost("/facturacion/api/quotes")]
    [Authorize(Policy = FacturacionPermissions.Crear)]
    public async Task<IActionResult> CreateQuoteJson(
        [FromBody] CreateQuoteJsonRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            return BadRequest(new { message = "El nombre del cliente es obligatorio." });
        }
        if (request.Lines is null || request.Lines.Count == 0 || request.Lines.All(l => string.IsNullOrWhiteSpace(l.Description)))
        {
            return BadRequest(new { message = "El presupuesto debe incluir al menos una línea con descripción." });
        }

        var lines = request.Lines
            .Where(l => !string.IsNullOrWhiteSpace(l.Description))
            .Select(l => new DocumentLineInputDto(
                Description: l.Description.Trim(),
                Quantity: l.Quantity,
                UnitPriceNet: l.UnitPriceNet,
                VatPercentage: l.VatPercentage,
                VatTaxId: l.VatTaxId,
                SurchargeTaxId: l.SurchargeTaxId,
                SurchargePercentage: l.SurchargePercentage ?? 0m))
            .ToList();

        var result = await facturacionService.CreateQuoteAsync(
            new SaveQuoteCommand(
                Id: null,
                SeriesId: request.SeriesId,
                IssueDate: request.IssueDate,
                ValidUntilDate: request.ValidUntilDate,
                CustomerName: request.CustomerName.Trim(),
                CustomerTaxId: request.CustomerTaxId?.Trim(),
                CustomerAddress: request.CustomerAddress?.Trim(),
                Notes: request.Notes?.Trim(),
                Lines: lines,
                WithholdingTaxId: request.WithholdingTaxId,
                WithholdingPercentage: request.WithholdingPercentage ?? 0m),
            cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.ErrorMessage ?? "No se pudo crear el presupuesto." });
        }

        return Ok(new { success = true, documentId = result.DocumentId, formattedNumber = result.FormattedNumber });
    }

    [HttpPost("/facturacion/api/draft-invoices")]
    [Authorize(Policy = FacturacionPermissions.Crear)]
    public async Task<IActionResult> CreateDraftInvoiceJson(
        [FromBody] CreateInvoiceJsonRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            return BadRequest(new { message = "El nombre del cliente o razón social es obligatorio." });
        }
        if (request.Lines is null || request.Lines.Count == 0 || request.Lines.All(l => string.IsNullOrWhiteSpace(l.Description)))
        {
            return BadRequest(new { message = "La factura debe incluir al menos una línea con descripción." });
        }

        var lines = request.Lines
            .Where(l => !string.IsNullOrWhiteSpace(l.Description))
            .Select(l => new DocumentLineInputDto(
                Description: l.Description.Trim(),
                Quantity: l.Quantity,
                UnitPriceNet: l.UnitPriceNet,
                VatPercentage: l.VatPercentage,
                VatTaxId: l.VatTaxId,
                SurchargeTaxId: l.SurchargeTaxId,
                SurchargePercentage: l.SurchargePercentage ?? 0m))
            .ToList();

        var result = await facturacionService.SaveDraftInvoiceAsync(
            new SaveDraftInvoiceCommand(
                Id: null,
                InvoiceType: request.InvoiceType,
                SeriesId: request.SeriesId,
                IssueDate: request.IssueDate,
                CustomerName: request.CustomerName.Trim(),
                CustomerTaxId: request.CustomerTaxId?.Trim(),
                CustomerAddress: request.CustomerAddress?.Trim(),
                PaymentMethod: request.PaymentMethod,
                Notes: request.Notes?.Trim(),
                Lines: lines,
                WithholdingTaxId: request.WithholdingTaxId,
                WithholdingPercentage: request.WithholdingPercentage ?? 0m),
            cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.ErrorMessage ?? "No se pudo guardar el borrador." });
        }

        return Ok(new { success = true, documentId = result.DocumentId, formattedNumber = result.FormattedNumber });
    }

    [HttpPost("/facturacion/api/invoices/{id:guid}/issue")]
    [Authorize(Policy = FacturacionPermissions.Emitir)]
    public async Task<IActionResult> IssueDraftInvoiceJson(
        Guid id,
        [FromBody] IssueInvoiceJsonRequest request,
        CancellationToken cancellationToken)
    {
        var result = await facturacionService.IssueDraftInvoiceAsync(id, request.RowVersion, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.ErrorMessage ?? "No se pudo emitir la factura." });
        }

        return Ok(new { success = true, formattedNumber = result.FormattedNumber });
    }

    [HttpPost("/facturacion/api/invoices/{id:guid}/rectify")]
    [Authorize(Policy = FacturacionPermissions.Rectificar)]
    public async Task<IActionResult> RectifyInvoiceJson(
        Guid id,
        [FromBody] RectifyInvoiceJsonRequest request,
        CancellationToken cancellationToken)
    {
        List<IssueInvoiceLineRequest>? diffLines = null;
        if (request.Scope == RectificationScope.PorDiferencias)
        {
            diffLines = request.DifferenceLines?
                .Where(l => !string.IsNullOrWhiteSpace(l.Description))
                .Select(l => new IssueInvoiceLineRequest(
                    l.Description.Trim(),
                    l.Quantity,
                    l.UnitPriceNet,
                    l.VatPercentage,
                    l.VatTaxId,
                    l.SurchargeTaxId,
                    l.SurchargePercentage ?? 0m))
                .ToList();
        }

        var result = await facturacionService.IssueRectifyingInvoiceAsync(
            new IssueRectifyingInvoiceRequest(
                OriginalInvoiceId: id,
                Scope: request.Scope,
                Reason: string.IsNullOrWhiteSpace(request.RectificationReason) ? "Rectificación" : request.RectificationReason.Trim(),
                PaymentMethod: request.PaymentMethod,
                DifferenceLines: diffLines,
                SeriesId: request.SeriesId),
            cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.ErrorMessage ?? "No se pudo rectificar la factura." });
        }

        return Ok(new { success = true, documentId = result.DocumentId, formattedNumber = result.FormattedNumber });
    }

    [HttpGet("/facturacion/presupuestos/crear")]
    [Authorize(Policy = FacturacionPermissions.Crear)]
    public async Task<IActionResult> CreateQuote(CancellationToken cancellationToken)
    {
        var vm = new QuoteFormViewModel
        {
            IssueDate = FiscalTimeZone.GetMadridDate(DateTimeOffset.UtcNow),
            ValidUntilDate = FiscalTimeZone.GetMadridDate(DateTimeOffset.UtcNow).AddDays(30),
            Lines =
            [
                new DocumentLineFormInputModel { Quantity = 1m, VatPercentage = 21m },
                new DocumentLineFormInputModel { Quantity = 1m, VatPercentage = 21m },
                new DocumentLineFormInputModel { Quantity = 1m, VatPercentage = 21m }
            ]
        };

        await PopulateQuoteFormCatalogsAsync(vm, cancellationToken);
        return View("QuoteForm", vm);
    }

    [HttpPost("/facturacion/presupuestos/crear")]
    [Authorize(Policy = FacturacionPermissions.Crear)]
    public async Task<IActionResult> CreateQuote(
        QuoteFormViewModel input,
        CancellationToken cancellationToken)
    {
        var lines = MapSubmittedLines(input.Lines);
        if (lines.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "El presupuesto debe incluir al menos una línea con descripción.");
        }

        if (!ModelState.IsValid)
        {
            EnsureMinimumLineRows(input.Lines);
            await PopulateQuoteFormCatalogsAsync(input, cancellationToken);
            return View("QuoteForm", input);
        }

        var result = await facturacionService.CreateQuoteAsync(
            new SaveQuoteCommand(
                Id: null,
                SeriesId: input.SeriesId,
                IssueDate: input.IssueDate,
                ValidUntilDate: input.ValidUntilDate,
                CustomerName: input.CustomerName,
                CustomerTaxId: input.CustomerTaxId,
                CustomerAddress: input.CustomerAddress,
                Notes: input.Notes,
                Lines: lines,
                WithholdingTaxId: input.WithholdingTaxId,
                WithholdingPercentage: input.WithholdingPercentage),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No se pudo crear el presupuesto.");
            EnsureMinimumLineRows(input.Lines);
            await PopulateQuoteFormCatalogsAsync(input, cancellationToken);
            return View("QuoteForm", input);
        }

        TempData["SuccessMessage"] = $"Presupuesto {result.FormattedNumber} creado correctamente.";
        return RedirectToAction(nameof(QuoteDetails), new { id = result.DocumentId });
    }

    [HttpGet("/facturacion/presupuestos/{id:guid}")]
    public async Task<IActionResult> QuoteDetails(Guid id, CancellationToken cancellationToken)
    {
        var quote = await facturacionService.GetQuoteByIdAsync(id, cancellationToken);
        if (quote is null)
        {
            TempData["ErrorMessage"] = "El presupuesto solicitado no existe.";
            return RedirectToAction(nameof(Index), new { tab = "presupuestos" });
        }

        var canCreate = (await authorizationService.AuthorizeAsync(User, FacturacionPermissions.Crear)).Succeeded;
        var canDownload = (await authorizationService.AuthorizeAsync(User, FacturacionPermissions.Descargar)).Succeeded;

        return View(new QuoteDetailsViewModel
        {
            Quote = quote,
            CanCreate = canCreate,
            CanDownload = canDownload
        });
    }

    [HttpGet("/facturacion/presupuestos/{id:guid}/editar")]
    [Authorize(Policy = FacturacionPermissions.Crear)]
    public async Task<IActionResult> EditQuote(Guid id, CancellationToken cancellationToken)
    {
        var quote = await facturacionService.GetQuoteByIdAsync(id, cancellationToken);
        if (quote is null)
        {
            TempData["ErrorMessage"] = "El presupuesto solicitado no existe.";
            return RedirectToAction(nameof(Index), new { tab = "presupuestos" });
        }

        var vm = new QuoteFormViewModel
        {
            Id = quote.Id,
            SeriesId = quote.SeriesId,
            IssueDate = quote.IssueDate,
            ValidUntilDate = quote.ValidUntilDate,
            CustomerName = quote.CustomerName,
            CustomerTaxId = quote.CustomerTaxId,
            CustomerAddress = quote.CustomerAddress,
            WithholdingTaxId = quote.WithholdingTaxId,
            WithholdingPercentage = quote.WithholdingPercentage,
            Notes = quote.Notes,
            RowVersion = quote.RowVersion,
            Lines = quote.Lines
                .Select(l => new DocumentLineFormInputModel
                {
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UnitPriceNet = l.UnitPriceNet,
                    VatTaxId = l.VatTaxId,
                    VatPercentage = l.VatPercentage,
                    SurchargeTaxId = l.SurchargeTaxId,
                    SurchargePercentage = l.SurchargePercentage
                })
                .ToList()
        };

        EnsureMinimumLineRows(vm.Lines);
        await PopulateQuoteFormCatalogsAsync(vm, cancellationToken);
        return View("QuoteForm", vm);
    }

    [HttpPost("/facturacion/presupuestos/{id:guid}/editar")]
    [Authorize(Policy = FacturacionPermissions.Crear)]
    public async Task<IActionResult> EditQuote(
        Guid id,
        QuoteFormViewModel input,
        CancellationToken cancellationToken)
    {
        if (input.Id != id)
        {
            return BadRequest();
        }

        var lines = MapSubmittedLines(input.Lines);
        if (lines.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "El presupuesto debe incluir al menos una línea con descripción.");
        }

        if (!ModelState.IsValid)
        {
            EnsureMinimumLineRows(input.Lines);
            await PopulateQuoteFormCatalogsAsync(input, cancellationToken);
            return View("QuoteForm", input);
        }

        var result = await facturacionService.UpdateQuoteAsync(
            new SaveQuoteCommand(
                Id: id,
                SeriesId: input.SeriesId,
                IssueDate: input.IssueDate,
                ValidUntilDate: input.ValidUntilDate,
                CustomerName: input.CustomerName,
                CustomerTaxId: input.CustomerTaxId,
                CustomerAddress: input.CustomerAddress,
                Notes: input.Notes,
                Lines: lines,
                WithholdingTaxId: input.WithholdingTaxId,
                WithholdingPercentage: input.WithholdingPercentage,
                RowVersion: input.RowVersion),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No se pudo actualizar el presupuesto.");
            EnsureMinimumLineRows(input.Lines);
            await PopulateQuoteFormCatalogsAsync(input, cancellationToken);
            return View("QuoteForm", input);
        }

        TempData["SuccessMessage"] = "Presupuesto actualizado correctamente.";
        return RedirectToAction(nameof(QuoteDetails), new { id });
    }

    [HttpPost("/facturacion/presupuestos/{id:guid}/estado")]
    [Authorize(Policy = FacturacionPermissions.Crear)]
    public async Task<IActionResult> ChangeQuoteStatus(
        Guid id,
        ChangeQuoteStatusInputModel input,
        CancellationToken cancellationToken)
    {
        if (id != input.Id)
        {
            return BadRequest();
        }

        var result = await facturacionService.ChangeQuoteStatusAsync(
            id,
            input.NewStatus,
            input.RowVersion,
            cancellationToken);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "No se pudo cambiar el estado del presupuesto.";
        }
        else
        {
            TempData["SuccessMessage"] = "Estado del presupuesto actualizado.";
        }

        return RedirectToAction(nameof(QuoteDetails), new { id });
    }

    [HttpPost("/facturacion/presupuestos/{id:guid}/convertir")]
    [Authorize(Policy = FacturacionPermissions.Crear)]
    public async Task<IActionResult> ConvertQuoteToInvoice(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await facturacionService.ConvertQuoteToDraftInvoiceAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "No se pudo convertir el presupuesto en borrador de factura.";
            return RedirectToAction(nameof(QuoteDetails), new { id });
        }

        TempData["SuccessMessage"] = "Presupuesto convertido en borrador de factura correctamente.";
        return RedirectToAction(nameof(InvoiceDetails), new { id = result.DocumentId });
    }

    [HttpGet("/facturacion/presupuestos/{id:guid}/pdf")]
    [Authorize(Policy = FacturacionPermissions.Descargar)]
    public async Task<IActionResult> DownloadQuotePdf(Guid id, CancellationToken cancellationToken)
    {
        var pdfResult = await facturacionService.DownloadQuotePdfAsync(id, cancellationToken);
        if (!pdfResult.Succeeded || pdfResult.PdfBytes is null)
        {
            TempData["ErrorMessage"] = pdfResult.ErrorMessage ?? "No se pudo generar el PDF del presupuesto.";
            return RedirectToAction(nameof(QuoteDetails), new { id });
        }

        return File(pdfResult.PdfBytes, "application/pdf", pdfResult.FileName ?? "Presupuesto.pdf");
    }

    [HttpGet("/facturacion/facturas/crear")]
    [Authorize(Policy = FacturacionPermissions.Crear)]
    public async Task<IActionResult> CreateInvoice(CancellationToken cancellationToken)
    {
        var vm = new InvoiceFormViewModel
        {
            InvoiceType = InvoiceDocumentType.Ordinaria,
            IssueDate = FiscalTimeZone.GetMadridDate(DateTimeOffset.UtcNow),
            PaymentMethod = BillingPaymentMethod.Contado,
            Lines =
            [
                new DocumentLineFormInputModel { Quantity = 1m, VatPercentage = 21m },
                new DocumentLineFormInputModel { Quantity = 1m, VatPercentage = 21m },
                new DocumentLineFormInputModel { Quantity = 1m, VatPercentage = 21m }
            ]
        };

        await PopulateInvoiceFormCatalogsAsync(vm, cancellationToken);
        return View("InvoiceForm", vm);
    }

    [HttpPost("/facturacion/facturas/crear")]
    [Authorize(Policy = FacturacionPermissions.Crear)]
    public async Task<IActionResult> CreateInvoice(
        InvoiceFormViewModel input,
        CancellationToken cancellationToken)
    {
        var lines = MapSubmittedLines(input.Lines);
        if (lines.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "El borrador de factura debe incluir al menos una línea con descripción.");
        }

        if (!ModelState.IsValid)
        {
            EnsureMinimumLineRows(input.Lines);
            await PopulateInvoiceFormCatalogsAsync(input, cancellationToken);
            return View("InvoiceForm", input);
        }

        var result = await facturacionService.SaveDraftInvoiceAsync(
            new SaveDraftInvoiceCommand(
                Id: null,
                InvoiceType: input.InvoiceType,
                SeriesId: input.SeriesId,
                IssueDate: input.IssueDate,
                CustomerName: input.CustomerName,
                CustomerTaxId: input.CustomerTaxId,
                CustomerAddress: input.CustomerAddress,
                PaymentMethod: input.PaymentMethod,
                Notes: input.Notes,
                Lines: lines,
                WithholdingTaxId: input.WithholdingTaxId,
                WithholdingPercentage: input.WithholdingPercentage),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No se pudo guardar el borrador de factura.");
            EnsureMinimumLineRows(input.Lines);
            await PopulateInvoiceFormCatalogsAsync(input, cancellationToken);
            return View("InvoiceForm", input);
        }

        TempData["SuccessMessage"] = "Borrador de factura guardado correctamente.";
        return RedirectToAction(nameof(InvoiceDetails), new { id = result.DocumentId });
    }

    [HttpGet("/facturacion/facturas/{id:guid}")]
    public async Task<IActionResult> InvoiceDetails(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await facturacionService.GetInvoiceByIdAsync(id, cancellationToken);
        if (invoice is null)
        {
            TempData["ErrorMessage"] = "La factura solicitada no existe.";
            return RedirectToAction(nameof(Index));
        }

        var settings = await facturacionService.GetSettingsAsync(cancellationToken);
        var canCreate = (await authorizationService.AuthorizeAsync(User, FacturacionPermissions.Crear)).Succeeded;
        var canIssue = (await authorizationService.AuthorizeAsync(User, FacturacionPermissions.Emitir)).Succeeded;
        var canRectify = (await authorizationService.AuthorizeAsync(User, FacturacionPermissions.Rectificar)).Succeeded;
        var canDownload = (await authorizationService.AuthorizeAsync(User, FacturacionPermissions.Descargar)).Succeeded;

        return View(new InvoiceDetailsViewModel
        {
            Invoice = invoice,
            Settings = settings,
            CanCreate = canCreate,
            CanIssue = canIssue,
            CanRectify = canRectify,
            CanDownload = canDownload
        });
    }

    [HttpGet("/facturacion/facturas/{id:guid}/editar")]
    [Authorize(Policy = FacturacionPermissions.Crear)]
    public async Task<IActionResult> EditInvoice(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await facturacionService.GetInvoiceByIdAsync(id, cancellationToken);
        if (invoice is null)
        {
            TempData["ErrorMessage"] = "El borrador solicitado no existe.";
            return RedirectToAction(nameof(Index));
        }

        if (invoice.Status != InvoiceStatus.Borrador)
        {
            TempData["ErrorMessage"] = "Una factura emitida no puede editarse. Utiliza una factura rectificativa.";
            return RedirectToAction(nameof(InvoiceDetails), new { id });
        }

        var vm = new InvoiceFormViewModel
        {
            Id = invoice.Id,
            InvoiceType = invoice.InvoiceType,
            SeriesId = invoice.SeriesId,
            IssueDate = invoice.IssueDate,
            CustomerName = invoice.CustomerName,
            CustomerTaxId = invoice.CustomerTaxId,
            CustomerAddress = invoice.CustomerAddress,
            PaymentMethod = invoice.PaymentMethod,
            WithholdingTaxId = invoice.WithholdingTaxId,
            WithholdingPercentage = invoice.WithholdingPercentage,
            Notes = invoice.Notes,
            RowVersion = invoice.RowVersion,
            Lines = invoice.Lines
                .Select(l => new DocumentLineFormInputModel
                {
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UnitPriceNet = l.UnitPriceNet,
                    VatTaxId = l.VatTaxId,
                    VatPercentage = l.VatPercentage,
                    SurchargeTaxId = l.SurchargeTaxId,
                    SurchargePercentage = l.SurchargePercentage
                })
                .ToList()
        };

        EnsureMinimumLineRows(vm.Lines);
        await PopulateInvoiceFormCatalogsAsync(vm, cancellationToken);
        return View("InvoiceForm", vm);
    }

    [HttpPost("/facturacion/facturas/{id:guid}/editar")]
    [Authorize(Policy = FacturacionPermissions.Crear)]
    public async Task<IActionResult> EditInvoice(
        Guid id,
        InvoiceFormViewModel input,
        CancellationToken cancellationToken)
    {
        if (input.Id != id)
        {
            return BadRequest();
        }

        var lines = MapSubmittedLines(input.Lines);
        if (lines.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "El borrador de factura debe incluir al menos una línea con descripción.");
        }

        if (!ModelState.IsValid)
        {
            EnsureMinimumLineRows(input.Lines);
            await PopulateInvoiceFormCatalogsAsync(input, cancellationToken);
            return View("InvoiceForm", input);
        }

        var result = await facturacionService.SaveDraftInvoiceAsync(
            new SaveDraftInvoiceCommand(
                Id: id,
                InvoiceType: input.InvoiceType,
                SeriesId: input.SeriesId,
                IssueDate: input.IssueDate,
                CustomerName: input.CustomerName,
                CustomerTaxId: input.CustomerTaxId,
                CustomerAddress: input.CustomerAddress,
                PaymentMethod: input.PaymentMethod,
                Notes: input.Notes,
                Lines: lines,
                WithholdingTaxId: input.WithholdingTaxId,
                WithholdingPercentage: input.WithholdingPercentage,
                RowVersion: input.RowVersion),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No se pudo actualizar el borrador de factura.");
            EnsureMinimumLineRows(input.Lines);
            await PopulateInvoiceFormCatalogsAsync(input, cancellationToken);
            return View("InvoiceForm", input);
        }

        TempData["SuccessMessage"] = "Borrador de factura actualizado correctamente.";
        return RedirectToAction(nameof(InvoiceDetails), new { id });
    }

    [HttpPost("/facturacion/facturas/{id:guid}/borrar")]
    [Authorize(Policy = FacturacionPermissions.Crear)]
    public async Task<IActionResult> DeleteDraftInvoice(Guid id, CancellationToken cancellationToken)
    {
        var result = await facturacionService.DeleteDraftInvoiceAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "No se pudo eliminar el borrador.";
            return RedirectToAction(nameof(InvoiceDetails), new { id });
        }

        TempData["SuccessMessage"] = "Borrador de factura eliminado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/facturacion/facturas/{id:guid}/emitir")]
    [Authorize(Policy = FacturacionPermissions.Emitir)]
    public async Task<IActionResult> IssueDraftInvoice(
        Guid id,
        IssueDraftInvoiceInputModel input,
        CancellationToken cancellationToken)
    {
        if (id != input.Id)
        {
            return BadRequest();
        }

        var result = await facturacionService.IssueDraftInvoiceAsync(id, input.RowVersion, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "No se pudo emitir la factura.";
            return RedirectToAction(nameof(InvoiceDetails), new { id });
        }

        TempData["SuccessMessage"] = $"Factura {result.FormattedNumber} emitida de forma definitiva e inmutable.";
        return RedirectToAction(nameof(InvoiceDetails), new { id });
    }

    [HttpGet("/facturacion/facturas/{id:guid}/rectificar")]
    [Authorize(Policy = FacturacionPermissions.Rectificar)]
    public async Task<IActionResult> RectifyInvoice(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await facturacionService.GetInvoiceByIdAsync(id, cancellationToken);
        if (invoice is null)
        {
            TempData["ErrorMessage"] = "La factura original solicitada no existe.";
            return RedirectToAction(nameof(Index));
        }

        if (invoice.Status == InvoiceStatus.Borrador)
        {
            TempData["ErrorMessage"] = "Esta factura no admite emisión de rectificativa en su estado actual.";
            return RedirectToAction(nameof(InvoiceDetails), new { id });
        }

        var vm = new RectifyInvoiceFormViewModel
        {
            OriginalInvoiceId = invoice.Id,
            OriginalFormattedNumber = invoice.FormattedNumber ?? "—",
            OriginalCustomerName = invoice.CustomerName,
            OriginalTotalGross = invoice.TotalGross,
            Scope = RectificationScope.Total,
            PaymentMethod = invoice.PaymentMethod,
            DifferenceLines =
            [
                new DocumentLineFormInputModel { Quantity = -1m, VatPercentage = 21m },
                new DocumentLineFormInputModel { Quantity = -1m, VatPercentage = 21m }
            ]
        };

        await PopulateRectifyCatalogsAsync(vm, cancellationToken);
        return View(vm);
    }

    [HttpPost("/facturacion/facturas/{id:guid}/rectificar")]
    [Authorize(Policy = FacturacionPermissions.Rectificar)]
    public async Task<IActionResult> RectifyInvoice(
        Guid id,
        RectifyInvoiceFormViewModel input,
        CancellationToken cancellationToken)
    {
        if (id != input.OriginalInvoiceId)
        {
            return BadRequest();
        }

        List<IssueInvoiceLineRequest>? diffLines = null;
        if (input.Scope == RectificationScope.PorDiferencias)
        {
            diffLines = input.DifferenceLines
                .Where(l => !string.IsNullOrWhiteSpace(l.Description))
                .Select(l => new IssueInvoiceLineRequest(
                    l.Description.Trim(),
                    l.Quantity,
                    l.UnitPriceNet,
                    l.VatPercentage,
                    l.VatTaxId,
                    l.SurchargeTaxId,
                    l.SurchargePercentage))
                .ToList();

            if (diffLines.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "La rectificación por diferencias requiere al menos una línea.");
            }
        }

        if (!ModelState.IsValid)
        {
            EnsureMinimumLineRows(input.DifferenceLines, 2);
            await PopulateRectifyCatalogsAsync(input, cancellationToken);
            return View(input);
        }

        var result = await facturacionService.IssueRectifyingInvoiceAsync(
            new IssueRectifyingInvoiceRequest(
                OriginalInvoiceId: id,
                Scope: input.Scope,
                Reason: input.Reason,
                PaymentMethod: input.PaymentMethod,
                DifferenceLines: diffLines,
                SeriesId: input.SeriesId),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No se pudo emitir la factura rectificativa.");
            EnsureMinimumLineRows(input.DifferenceLines, 2);
            await PopulateRectifyCatalogsAsync(input, cancellationToken);
            return View(input);
        }

        TempData["SuccessMessage"] = $"Factura rectificativa {result.FormattedNumber} emitida correctamente.";
        return RedirectToAction(nameof(InvoiceDetails), new { id = result.DocumentId });
    }

    [HttpGet("/facturacion/facturas/{id:guid}/pdf")]
    [Authorize(Policy = FacturacionPermissions.Descargar)]
    public async Task<IActionResult> DownloadInvoicePdf(Guid id, CancellationToken cancellationToken)
    {
        var pdfResult = await facturacionService.DownloadInvoicePdfAsync(id, cancellationToken);
        if (!pdfResult.Succeeded || pdfResult.PdfBytes is null)
        {
            TempData["ErrorMessage"] = pdfResult.ErrorMessage ?? "No se pudo generar el PDF de la factura.";
            return RedirectToAction(nameof(InvoiceDetails), new { id });
        }

        return File(pdfResult.PdfBytes, "application/pdf", pdfResult.FileName ?? "Factura.pdf");
    }

    [HttpGet("/facturacion/configuracion")]
    [Authorize(Policy = FacturacionPermissions.Configurar)]
    public async Task<IActionResult> Settings(CancellationToken cancellationToken)
    {
        var vm = await BuildSettingsViewModelAsync(null, null, null, cancellationToken);
        return View(vm);
    }

    [HttpPost("/facturacion/configuracion/emisor")]
    [Authorize(Policy = FacturacionPermissions.Configurar)]
    public async Task<IActionResult> UpdateIssuerSettings(
        [Bind(Prefix = "IssuerInput")] BillingSettingsInputModel input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var invalidVm = await BuildSettingsViewModelAsync(input, null, null, cancellationToken);
            return View("Settings", invalidVm);
        }

        var result = await facturacionService.UpdateSettingsAsync(
            new UpdateBillingSettingsCommand(
                TaxName: input.TaxName,
                TaxId: input.TaxId,
                FiscalAddress: input.FiscalAddress,
                LogoPrivateFileId: input.LogoPrivateFileId,
                LegalTermsText: input.LegalTermsText,
                FooterText: input.FooterText,
                RowVersion: input.RowVersion),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No se pudo actualizar la configuración del emisor.");
            var errorVm = await BuildSettingsViewModelAsync(input, null, null, cancellationToken);
            return View("Settings", errorVm);
        }

        TempData["SuccessMessage"] = "Datos fiscales del emisor actualizados correctamente.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost("/facturacion/configuracion/series")]
    [Authorize(Policy = FacturacionPermissions.Configurar)]
    public async Task<IActionResult> SaveSeries(
        [Bind(Prefix = "NewSeriesInput")] SaveBillingSeriesInputModel input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var invalidVm = await BuildSettingsViewModelAsync(null, input, null, cancellationToken);
            return View("Settings", invalidVm);
        }

        var result = await facturacionService.SaveSeriesAsync(
            new SaveBillingSeriesCommand(
                Id: input.Id,
                Code: input.Code,
                Name: input.Name,
                DocumentKind: input.DocumentKind,
                FiscalYear: input.FiscalYear,
                PaddingDigits: input.PaddingDigits,
                IsActive: input.IsActive,
                IsDefault: input.IsDefault),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No se pudo guardar la serie de facturación.");
            var errorVm = await BuildSettingsViewModelAsync(null, input, null, cancellationToken);
            return View("Settings", errorVm);
        }

        TempData["SuccessMessage"] = "Serie de facturación guardada correctamente.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost("/facturacion/configuracion/impuestos/crear")]
    [Authorize(Policy = FacturacionPermissions.Configurar)]
    public async Task<IActionResult> CreateTax(
        [Bind(Prefix = "NewTaxInput")] CreateTaxRateInputModel input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var invalidVm = await BuildSettingsViewModelAsync(null, null, input, cancellationToken);
            return View("Settings", invalidVm);
        }

        var result = await facturacionService.CreateTaxAsync(
            new CreateTaxRateCommand(
                Code: input.Code,
                Name: input.Name,
                Kind: input.Kind,
                Percentage: input.Percentage,
                ParentVatTaxId: input.ParentVatTaxId,
                Applicability: input.Applicability,
                IsDefault: input.IsDefault),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No se pudo crear el impuesto.");
            var errorVm = await BuildSettingsViewModelAsync(null, null, input, cancellationToken);
            return View("Settings", errorVm);
        }

        TempData["SuccessMessage"] = "Impuesto añadido al catálogo correctamente.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost("/facturacion/configuracion/impuestos/editar")]
    [Authorize(Policy = FacturacionPermissions.Configurar)]
    public async Task<IActionResult> UpdateTax(
        UpdateTaxRateInputModel input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Datos de actualización de impuesto no válidos.";
            return RedirectToAction(nameof(Settings));
        }

        var result = await facturacionService.UpdateTaxAsync(
            new UpdateTaxRateCommand(
                Id: input.Id,
                Name: input.Name,
                Applicability: input.Applicability,
                IsActive: input.IsActive,
                IsDefault: input.IsDefault,
                RowVersion: input.RowVersion),
            cancellationToken);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "No se pudo actualizar el impuesto.";
        }
        else
        {
            TempData["SuccessMessage"] = "Impuesto actualizado correctamente.";
        }

        return RedirectToAction(nameof(Settings));
    }

    private static List<DocumentLineInputDto> MapSubmittedLines(IEnumerable<DocumentLineFormInputModel>? rawLines)
    {
        if (rawLines is null)
        {
            return [];
        }

        return rawLines
            .Where(l => !string.IsNullOrWhiteSpace(l.Description))
            .Select(l => new DocumentLineInputDto(
                Description: l.Description.Trim(),
                Quantity: l.Quantity,
                UnitPriceNet: l.UnitPriceNet,
                VatPercentage: l.VatPercentage,
                VatTaxId: l.VatTaxId,
                SurchargeTaxId: l.SurchargeTaxId,
                SurchargePercentage: l.SurchargePercentage))
            .ToList();
    }

    private static void EnsureMinimumLineRows(List<DocumentLineFormInputModel> lines, int minRows = 3)
    {
        while (lines.Count < minRows)
        {
            lines.Add(new DocumentLineFormInputModel { Quantity = 1m, VatPercentage = 21m });
        }
    }

    private async Task PopulateQuoteFormCatalogsAsync(QuoteFormViewModel vm, CancellationToken cancellationToken)
    {
        var currentYear = FiscalTimeZone.GetMadridFiscalYear(DateTimeOffset.UtcNow);
        await facturacionService.EnsureDefaultSeriesForYearAsync(BillingDocumentKind.Presupuesto, currentYear, cancellationToken);

        var allSeries = await facturacionService.GetSeriesAsync(null, cancellationToken);
        var activeTaxes = await facturacionService.GetTaxesAsync(includeInactive: false, cancellationToken);

        vm.AvailableSeries = allSeries.Where(s => s.DocumentKind == BillingDocumentKind.Presupuesto && s.IsActive).ToList();
        vm.AvailableVatTaxes = activeTaxes.Where(t => t.Kind == TaxKind.Iva).ToList();
        vm.AvailableSurchargeTaxes = activeTaxes.Where(t => t.Kind == TaxKind.RecargoEquivalencia).ToList();
        vm.AvailableWithholdingTaxes = activeTaxes.Where(t => t.Kind == TaxKind.Irpf).ToList();
    }

    private async Task PopulateInvoiceFormCatalogsAsync(InvoiceFormViewModel vm, CancellationToken cancellationToken)
    {
        var currentYear = FiscalTimeZone.GetMadridFiscalYear(DateTimeOffset.UtcNow);
        await facturacionService.EnsureDefaultSeriesForYearAsync(BillingDocumentKind.Ordinaria, currentYear, cancellationToken);
        await facturacionService.EnsureDefaultSeriesForYearAsync(BillingDocumentKind.Simplificada, currentYear, cancellationToken);

        var allSeries = await facturacionService.GetSeriesAsync(null, cancellationToken);
        var activeTaxes = await facturacionService.GetTaxesAsync(includeInactive: false, cancellationToken);

        vm.AvailableSeries = allSeries
            .Where(s => (s.DocumentKind == BillingDocumentKind.Ordinaria || s.DocumentKind == BillingDocumentKind.Simplificada) && s.IsActive)
            .ToList();
        vm.AvailableVatTaxes = activeTaxes.Where(t => t.Kind == TaxKind.Iva).ToList();
        vm.AvailableSurchargeTaxes = activeTaxes.Where(t => t.Kind == TaxKind.RecargoEquivalencia).ToList();
        vm.AvailableWithholdingTaxes = activeTaxes.Where(t => t.Kind == TaxKind.Irpf).ToList();
    }

    private async Task PopulateRectifyCatalogsAsync(RectifyInvoiceFormViewModel vm, CancellationToken cancellationToken)
    {
        var currentYear = FiscalTimeZone.GetMadridFiscalYear(DateTimeOffset.UtcNow);
        await facturacionService.EnsureDefaultSeriesForYearAsync(BillingDocumentKind.Rectificativa, currentYear, cancellationToken);

        var allSeries = await facturacionService.GetSeriesAsync(null, cancellationToken);
        var activeTaxes = await facturacionService.GetTaxesAsync(includeInactive: false, cancellationToken);

        vm.AvailableSeries = allSeries
            .Where(s => s.DocumentKind == BillingDocumentKind.Rectificativa && s.IsActive)
            .ToList();
        vm.AvailableVatTaxes = activeTaxes.Where(t => t.Kind == TaxKind.Iva).ToList();
        vm.AvailableSurchargeTaxes = activeTaxes.Where(t => t.Kind == TaxKind.RecargoEquivalencia).ToList();
    }

    private async Task<FacturacionSettingsViewModel> BuildSettingsViewModelAsync(
        BillingSettingsInputModel? issuerOverride,
        SaveBillingSeriesInputModel? seriesOverride,
        CreateTaxRateInputModel? taxOverride,
        CancellationToken cancellationToken)
    {
        var settings = await facturacionService.GetSettingsAsync(cancellationToken);
        var currentYear = FiscalTimeZone.GetMadridFiscalYear(DateTimeOffset.UtcNow);

        await facturacionService.EnsureDefaultSeriesForYearAsync(BillingDocumentKind.Presupuesto, currentYear, cancellationToken);
        await facturacionService.EnsureDefaultSeriesForYearAsync(BillingDocumentKind.Ordinaria, currentYear, cancellationToken);
        await facturacionService.EnsureDefaultSeriesForYearAsync(BillingDocumentKind.Rectificativa, currentYear, cancellationToken);

        var series = await facturacionService.GetSeriesAsync(null, cancellationToken);
        var taxes = await facturacionService.GetTaxesAsync(includeInactive: true, cancellationToken);

        return new FacturacionSettingsViewModel
        {
            IssuerInput = issuerOverride ?? new BillingSettingsInputModel
            {
                TaxName = settings.TaxName,
                TaxId = settings.TaxId,
                FiscalAddress = settings.FiscalAddress,
                LogoPrivateFileId = settings.LogoPrivateFileId,
                LegalTermsText = settings.LegalTermsText,
                FooterText = settings.FooterText,
                RowVersion = settings.RowVersion
            },
            NewSeriesInput = seriesOverride ?? new SaveBillingSeriesInputModel
            {
                DocumentKind = BillingDocumentKind.Ordinaria,
                FiscalYear = currentYear,
                PaddingDigits = 5,
                IsActive = true
            },
            NewTaxInput = taxOverride ?? new CreateTaxRateInputModel
            {
                Kind = TaxKind.Iva,
                Applicability = TaxApplicability.Ambos
            },
            Series = series,
            Taxes = taxes
        };
    }
}
