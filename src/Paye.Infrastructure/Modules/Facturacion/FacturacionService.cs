using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Paye.Core.Auditing;
using Paye.Core.Common;
using Paye.Core.Demo;
using Paye.Core.Files;
using Paye.Core.Invoicing;
using Paye.Core.Modules.Facturacion;
using Paye.Infrastructure.Data;

namespace Paye.Infrastructure.Modules.Facturacion;

public static class FiscalTimeZone
{
    private static readonly TimeZoneInfo MadridZone = ResolveMadridTimeZone();

    public static DateTimeOffset ConvertToMadrid(DateTimeOffset utcTimestamp) =>
        TimeZoneInfo.ConvertTime(utcTimestamp, MadridZone);

    public static DateOnly GetMadridDate(DateTimeOffset utcTimestamp)
    {
        var madridTime = ConvertToMadrid(utcTimestamp);
        return DateOnly.FromDateTime(madridTime.DateTime);
    }

    public static int GetMadridFiscalYear(DateTimeOffset utcTimestamp) =>
        GetMadridDate(utcTimestamp).Year;

    private static TimeZoneInfo ResolveMadridTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
        }
    }
}

public sealed class FacturacionService(
    PayeDbContext dbContext,
    ICurrentUserService currentUserService,
    IAuditLogger auditLogger,
    IPrivateFileStorage privateFileStorage,
    IPdfInvoiceGenerator pdfInvoiceGenerator,
    IFiscalInvoiceRegistry fiscalInvoiceRegistry)
    : IFacturacionService, IInvoiceIssuer, ITaxCatalogReader
{
    public async Task<BillingSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await EnsureSettingsEntityAsync(cancellationToken);
        return MapSettingsToDto(settings);
    }

    public async Task<FacturacionOperationResult> UpdateSettingsAsync(
        UpdateBillingSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentUserService.IsDemoSandboxUser)
        {
            return FacturacionOperationResult.DemoBlocked(
                "El modo demostración no permite modificar la configuración fiscal del emisor.");
        }

        var taxName = command.TaxName?.Trim() ?? string.Empty;
        var taxId = command.TaxId?.Trim() ?? string.Empty;
        var fiscalAddress = command.FiscalAddress?.Trim() ?? string.Empty;
        var legalTerms = command.LegalTermsText?.Trim() ?? string.Empty;
        var footerText = command.FooterText?.Trim() ?? string.Empty;

        if (taxName.Length > 200)
        {
            return FacturacionOperationResult.Validation("La razón social no puede superar los 200 caracteres.");
        }

        if (fiscalAddress.Length > 500)
        {
            return FacturacionOperationResult.Validation("El domicilio fiscal no puede superar los 500 caracteres.");
        }

        if (!string.IsNullOrWhiteSpace(taxId))
        {
            if (!SpanishTaxIdValidator.IsValid(taxId, out var taxIdError))
            {
                return FacturacionOperationResult.Validation(
                    taxIdError ?? "El NIF/CIF/NIE del emisor no es válido.");
            }

            taxId = SpanishTaxIdValidator.Normalize(taxId);
        }

        if (command.LogoPrivateFileId.HasValue)
        {
            var (logoBytes, logoError) = await TryLoadAndValidateLogoBytesAsync(
                command.LogoPrivateFileId.Value,
                cancellationToken);
            if (logoError is not null || logoBytes is null)
            {
                return FacturacionOperationResult.Validation(
                    logoError ?? "El archivo de logotipo indicado no existe o no es válido.");
            }
        }

        var settings = await EnsureSettingsEntityAsync(cancellationToken);
        dbContext.Entry(settings).Property(x => x.RowVersion).OriginalValue = command.RowVersion;

        settings.TaxName = taxName;
        settings.TaxId = taxId;
        settings.FiscalAddress = fiscalAddress;
        settings.LogoPrivateFileId = command.LogoPrivateFileId;
        settings.LegalTermsText = legalTerms;
        settings.FooterText = footerText;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return FacturacionOperationResult.Conflict();
        }

        await auditLogger.LogCriticalActionAsync(
            action: "Facturacion.Settings.Update",
            entityName: nameof(BillingSettings),
            entityId: settings.Id.ToString(),
            summary: $"Configuración fiscal actualizada (NIF='{settings.TaxId}', RazónSocial='{settings.TaxName}').",
            cancellationToken: cancellationToken);

        return FacturacionOperationResult.Ok(settings.Id);
    }

    public async Task<IReadOnlyList<TaxDefinitionDto>> GetTaxesAsync(
        bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultTaxCatalogAsync(cancellationToken);

        var query = dbContext.Set<TaxRateDefinition>().AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        var taxes = await query
            .OrderBy(t => t.Kind)
            .ThenByDescending(t => t.Percentage)
            .ThenBy(t => t.Code)
            .ToListAsync(cancellationToken);

        var byId = taxes.ToDictionary(t => t.Id);
        if (!includeInactive && taxes.Any(t => t.ParentVatTaxId.HasValue && !byId.ContainsKey(t.ParentVatTaxId.Value)))
        {
            var allTaxes = await dbContext.Set<TaxRateDefinition>().AsNoTracking().ToListAsync(cancellationToken);
            byId = allTaxes.ToDictionary(t => t.Id);
        }

        return taxes.Select(t => new TaxDefinitionDto(
            Id: t.Id,
            Code: t.Code,
            Name: t.Name,
            Kind: t.Kind,
            Percentage: t.Percentage,
            ParentVatTaxId: t.ParentVatTaxId,
            ParentVatPercentage: t.ParentVatTaxId.HasValue && byId.TryGetValue(t.ParentVatTaxId.Value, out var parent)
                ? parent.Percentage
                : null,
            Applicability: t.Applicability,
            AeatTaxCode: t.AeatTaxCode,
            AeatRegimeKey: t.AeatRegimeKey,
            AeatOperationQualification: t.AeatOperationQualification,
            IsActive: t.IsActive,
            IsDefault: t.IsDefault,
            RowVersion: t.RowVersion)).ToList();
    }

    public async Task<IReadOnlyList<TaxDefinitionDto>> GetActiveTaxesAsync(
        TaxApplicability? applicabilityFilter = null,
        CancellationToken cancellationToken = default)
    {
        var active = await GetTaxesAsync(includeInactive: false, cancellationToken: cancellationToken);
        if (!applicabilityFilter.HasValue || applicabilityFilter.Value == TaxApplicability.Ambos)
        {
            return active;
        }

        return active
            .Where(t => t.Applicability == TaxApplicability.Ambos || t.Applicability == applicabilityFilter.Value)
            .ToList();
    }

    public async Task<FacturacionOperationResult> CreateTaxAsync(
        CreateTaxRateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentUserService.IsDemoSandboxUser)
        {
            return FacturacionOperationResult.DemoBlocked(
                "El modo demostración no permite modificar el catálogo de impuestos.");
        }

        await EnsureDefaultTaxCatalogAsync(cancellationToken);

        var code = command.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        var name = command.Name?.Trim() ?? string.Empty;
        var pct = InvoiceTotalsCalculator.RoundMonetary(command.Percentage, 2);

        if (string.IsNullOrWhiteSpace(code) || code.Length > 32)
        {
            return FacturacionOperationResult.Validation("El código del impuesto es obligatorio y no puede superar 32 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            return FacturacionOperationResult.Validation("El nombre del impuesto es obligatorio y no puede superar 120 caracteres.");
        }

        if (pct < 0m || pct > 100m)
        {
            return FacturacionOperationResult.Validation("El porcentaje del impuesto debe estar entre 0% y 100%.");
        }

        var existsCode = await dbContext.Set<TaxRateDefinition>()
            .AnyAsync(t => t.Code == code, cancellationToken);
        if (existsCode)
        {
            return FacturacionOperationResult.Validation($"Ya existe un impuesto con el código '{code}'.");
        }

        if (command.Kind == TaxKind.RecargoEquivalencia)
        {
            if (!command.ParentVatTaxId.HasValue)
            {
                return FacturacionOperationResult.Validation(
                    "Un tipo de Recargo de Equivalencia debe estar vinculado obligatoriamente a un tipo de IVA padre.");
            }

            var parentVat = await dbContext.Set<TaxRateDefinition>()
                .FirstOrDefaultAsync(t => t.Id == command.ParentVatTaxId.Value && t.Kind == TaxKind.Iva, cancellationToken);
            if (parentVat is null)
            {
                return FacturacionOperationResult.Validation("El tipo de IVA padre seleccionado para el Recargo de Equivalencia no existe.");
            }
        }
        else if (command.ParentVatTaxId.HasValue)
        {
            return FacturacionOperationResult.Validation("Solo los impuestos de tipo Recargo de Equivalencia pueden vincularse a un IVA padre.");
        }

        if (command.IsDefault)
        {
            var currentDefaults = await dbContext.Set<TaxRateDefinition>()
                .Where(t => t.Kind == command.Kind && t.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (var item in currentDefaults)
            {
                item.IsDefault = false;
            }
        }

        var aeatTaxCode = string.IsNullOrWhiteSpace(command.AeatTaxCode)
            ? (command.Kind == TaxKind.Irpf ? "IRPF" : "01")
            : command.AeatTaxCode.Trim();
        var aeatRegimeKey = string.IsNullOrWhiteSpace(command.AeatRegimeKey)
            ? (command.Kind == TaxKind.RecargoEquivalencia ? "18" : "01")
            : command.AeatRegimeKey.Trim();
        var aeatQualification = string.IsNullOrWhiteSpace(command.AeatOperationQualification)
            ? (command.Kind == TaxKind.Irpf ? "NA" : (pct == 0m ? "E1" : "S1"))
            : command.AeatOperationQualification.Trim();

        var entity = new TaxRateDefinition
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Kind = command.Kind,
            Percentage = pct,
            ParentVatTaxId = command.ParentVatTaxId,
            Applicability = command.Applicability,
            AeatTaxCode = aeatTaxCode,
            AeatRegimeKey = aeatRegimeKey,
            AeatOperationQualification = aeatQualification,
            IsActive = true,
            IsDefault = command.IsDefault
        };

        dbContext.Set<TaxRateDefinition>().Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.LogCriticalActionAsync(
            action: "Facturacion.Tax.Create",
            entityName: nameof(TaxRateDefinition),
            entityId: entity.Id.ToString(),
            summary: $"Impuesto creado: {entity.Code} ({entity.Name} - {entity.Percentage.ToString("0.##", CultureInfo.InvariantCulture)}%).",
            cancellationToken: cancellationToken);

        return FacturacionOperationResult.Ok(entity.Id, entity.Code);
    }

    public async Task<FacturacionOperationResult> UpdateTaxAsync(
        UpdateTaxRateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentUserService.IsDemoSandboxUser)
        {
            return FacturacionOperationResult.DemoBlocked(
                "El modo demostración no permite modificar el catálogo de impuestos.");
        }

        var name = command.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            return FacturacionOperationResult.Validation("El nombre del impuesto es obligatorio y no puede superar 120 caracteres.");
        }

        var tax = await dbContext.Set<TaxRateDefinition>()
            .FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken);
        if (tax is null)
        {
            return FacturacionOperationResult.NotFoundResult("El impuesto solicitado no existe en el catálogo.");
        }

        dbContext.Entry(tax).Property(x => x.RowVersion).OriginalValue = command.RowVersion;

        if (command.IsDefault && !tax.IsDefault)
        {
            var otherDefaults = await dbContext.Set<TaxRateDefinition>()
                .Where(t => t.Kind == tax.Kind && t.Id != tax.Id && t.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (var other in otherDefaults)
            {
                other.IsDefault = false;
            }
        }

        tax.Name = name;
        tax.Applicability = command.Applicability;
        tax.IsActive = command.IsActive;
        tax.IsDefault = command.IsActive && command.IsDefault;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return FacturacionOperationResult.Conflict();
        }

        await auditLogger.LogCriticalActionAsync(
            action: "Facturacion.Tax.Update",
            entityName: nameof(TaxRateDefinition),
            entityId: tax.Id.ToString(),
            summary: $"Impuesto actualizado: {tax.Code} (Nombre='{tax.Name}', Activo={tax.IsActive}, Aplicabilidad={tax.Applicability}).",
            cancellationToken: cancellationToken);

        return FacturacionOperationResult.Ok(tax.Id, tax.Code);
    }

    public async Task<IReadOnlyList<BillingSeriesDto>> GetSeriesAsync(
        int? fiscalYear = null,
        CancellationToken cancellationToken = default)
    {
        var currentYear = fiscalYear ?? FiscalTimeZone.GetMadridFiscalYear(DateTimeOffset.UtcNow);
        await EnsureDefaultSeriesForYearAsync(BillingDocumentKind.Presupuesto, currentYear, cancellationToken);
        await EnsureDefaultSeriesForYearAsync(BillingDocumentKind.Ordinaria, currentYear, cancellationToken);
        await EnsureDefaultSeriesForYearAsync(BillingDocumentKind.Rectificativa, currentYear, cancellationToken);

        var query = ApplySeriesSandboxIsolation(dbContext.Set<BillingSeries>().AsNoTracking());
        if (fiscalYear.HasValue)
        {
            query = query.Where(s => s.FiscalYear == fiscalYear.Value);
        }

        return await query
            .OrderByDescending(s => s.FiscalYear)
            .ThenBy(s => s.DocumentKind)
            .ThenBy(s => s.Code)
            .Select(s => new BillingSeriesDto(
                s.Id,
                s.Code,
                s.Name,
                s.DocumentKind,
                s.FiscalYear,
                s.LastNumber,
                s.PaddingDigits,
                s.IsActive,
                s.IsDefault,
                s.RowVersion))
            .ToListAsync(cancellationToken);
    }

    public async Task<BillingSeriesDto> EnsureDefaultSeriesForYearAsync(
        BillingDocumentKind kind,
        int fiscalYear,
        CancellationToken cancellationToken = default)
    {
        var existing = await ApplySeriesSandboxIsolation(dbContext.Set<BillingSeries>().AsNoTracking())
            .Where(s => s.DocumentKind == kind && s.FiscalYear == fiscalYear && s.IsActive)
            .OrderByDescending(s => s.IsDefault)
            .ThenBy(s => s.Code)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return MapSeriesToDto(existing);
        }

        var (baseCode, defaultName) = kind switch
        {
            BillingDocumentKind.Presupuesto => ("P", "Presupuestos"),
            BillingDocumentKind.Ordinaria => ("F", "Facturas Ordinarias"),
            BillingDocumentKind.Simplificada => ("S", "Facturas Simplificadas"),
            BillingDocumentKind.Rectificativa => ("R", "Facturas Rectificativas"),
            _ => ("F", "Facturas")
        };

        var code = currentUserService.IsDemoSandboxUser
            ? $"{baseCode}D{GetDemoSeriesSuffix()}"
            : baseCode;

        var series = new BillingSeries
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = $"{defaultName} {fiscalYear.ToString(CultureInfo.InvariantCulture)}",
            DocumentKind = kind,
            FiscalYear = fiscalYear,
            LastNumber = 0,
            PaddingDigits = 4,
            IsActive = true,
            IsDefault = true
        };

        dbContext.Set<BillingSeries>().Add(series);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(series).State = EntityState.Detached;
            var concurrent = await ApplySeriesSandboxIsolation(dbContext.Set<BillingSeries>().AsNoTracking())
                .FirstAsync(s => s.DocumentKind == kind && s.FiscalYear == fiscalYear && s.IsActive, cancellationToken);
            return MapSeriesToDto(concurrent);
        }

        return MapSeriesToDto(series);
    }

    public async Task<FacturacionOperationResult> SaveSeriesAsync(
        SaveBillingSeriesCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentUserService.IsDemoSandboxUser)
        {
            return FacturacionOperationResult.DemoBlocked(
                "El modo demostración no permite crear ni modificar series fiscales.");
        }

        var code = command.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        var name = command.Name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(code) || code.Length > 12 || code.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            return FacturacionOperationResult.Validation(
                "El código de serie es obligatorio, alfanumérico y de hasta 12 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            return FacturacionOperationResult.Validation(
                "El nombre de la serie es obligatorio y no puede superar 120 caracteres.");
        }

        if (command.FiscalYear < 2020 || command.FiscalYear > 2100)
        {
            return FacturacionOperationResult.Validation("El ejercicio fiscal debe estar entre 2020 y 2100.");
        }

        var padding = Math.Clamp(command.PaddingDigits, 1, 8);

        if (command.Id.HasValue)
        {
            var existing = await ApplySeriesSandboxIsolation(dbContext.Set<BillingSeries>())
                .FirstOrDefaultAsync(s => s.Id == command.Id.Value, cancellationToken);
            if (existing is null)
            {
                return FacturacionOperationResult.NotFoundResult("La serie indicada no existe.");
            }

            if (existing.LastNumber > 0 &&
                (!string.Equals(existing.Code, code, StringComparison.Ordinal) ||
                 existing.FiscalYear != command.FiscalYear ||
                 existing.DocumentKind != command.DocumentKind))
            {
                return FacturacionOperationResult.Validation(
                    "No se puede alterar el código, tipo ni ejercicio de una serie que ya ha emitido numeración.");
            }

            existing.Code = code;
            existing.Name = name;
            existing.DocumentKind = command.DocumentKind;
            existing.FiscalYear = command.FiscalYear;
            existing.PaddingDigits = padding;
            existing.IsActive = command.IsActive;
            existing.IsDefault = command.IsDefault;

            await dbContext.SaveChangesAsync(cancellationToken);
            return FacturacionOperationResult.Ok(existing.Id, existing.Code);
        }

        var duplicate = await dbContext.Set<BillingSeries>()
            .AnyAsync(s => s.Code == code && s.FiscalYear == command.FiscalYear, cancellationToken);
        if (duplicate)
        {
            return FacturacionOperationResult.Validation(
                $"Ya existe la serie '{code}' para el ejercicio {command.FiscalYear.ToString(CultureInfo.InvariantCulture)}.");
        }

        var created = new BillingSeries
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            DocumentKind = command.DocumentKind,
            FiscalYear = command.FiscalYear,
            LastNumber = 0,
            PaddingDigits = padding,
            IsActive = command.IsActive,
            IsDefault = command.IsDefault
        };

        dbContext.Set<BillingSeries>().Add(created);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.LogCriticalActionAsync(
            action: "Facturacion.Series.Create",
            entityName: nameof(BillingSeries),
            entityId: created.Id.ToString(),
            summary: $"Serie creada: {created.Code}-{created.FiscalYear.ToString(CultureInfo.InvariantCulture)} ({created.DocumentKind}).",
            cancellationToken: cancellationToken);

        return FacturacionOperationResult.Ok(created.Id, created.Code);
    }

    public async Task<IReadOnlyList<QuoteDto>> GetQuotesAsync(CancellationToken cancellationToken = default)
    {
        var quotes = await ApplyQuoteSandboxIsolation(dbContext.Set<Quote>().AsNoTracking())
            .Include(q => q.Lines.OrderBy(l => l.LineOrder))
            .OrderByDescending(q => q.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return quotes.Select(MapQuoteToDto).ToList();
    }

    public async Task<QuoteDto?> GetQuoteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quote = await ApplyQuoteSandboxIsolation(dbContext.Set<Quote>().AsNoTracking())
            .Include(q => q.Lines.OrderBy(l => l.LineOrder))
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        return quote is null ? null : MapQuoteToDto(quote);
    }

    public async Task<FacturacionOperationResult> CreateQuoteAsync(
        SaveQuoteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validationError = ValidateCustomerAndLines(
            command.CustomerName,
            command.CustomerTaxId,
            command.CustomerAddress,
            requireFullCustomerFiscalData: false,
            command.Lines,
            out var normalizedTaxId);
        if (validationError is not null)
        {
            return FacturacionOperationResult.Validation(validationError);
        }

        var (calculated, withholdingTax, calcError) = await ResolveAndCalculateTotalsAsync(
            command.Lines,
            command.WithholdingTaxId,
            command.WithholdingPercentage,
            allowNegativeLines: false,
            cancellationToken);
        if (calcError is not null || calculated is null)
        {
            return FacturacionOperationResult.Validation(calcError ?? "No se pudieron calcular los importes del presupuesto.");
        }

        var issueDate = command.IssueDate ?? FiscalTimeZone.GetMadridDate(DateTimeOffset.UtcNow);
        var fiscalYear = issueDate.Year;

        var seriesDto = command.SeriesId.HasValue
            ? await GetSeriesByIdAndKindAsync(command.SeriesId.Value, BillingDocumentKind.Presupuesto, cancellationToken)
            : await EnsureDefaultSeriesForYearAsync(BillingDocumentKind.Presupuesto, fiscalYear, cancellationToken);

        if (seriesDto is null)
        {
            return FacturacionOperationResult.Validation("La serie seleccionada no es válida para presupuestos.");
        }

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var (seqNumber, formattedNumber, allocError) = await AllocateNextSeriesNumberAsync(
            seriesDto.Id,
            BillingDocumentKind.Presupuesto,
            cancellationToken);
        if (allocError is not null || !seqNumber.HasValue || formattedNumber is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return FacturacionOperationResult.Validation(allocError ?? "No se pudo numerar el presupuesto.");
        }

        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            SeriesId = seriesDto.Id,
            SeriesCode = seriesDto.Code,
            FiscalYear = seriesDto.FiscalYear,
            SequentialNumber = seqNumber.Value,
            FormattedNumber = formattedNumber,
            Status = QuoteStatus.Borrador,
            IssueDate = issueDate,
            ValidUntilDate = command.ValidUntilDate,
            CustomerName = command.CustomerName.Trim(),
            CustomerTaxId = normalizedTaxId,
            CustomerAddress = string.IsNullOrWhiteSpace(command.CustomerAddress) ? null : command.CustomerAddress.Trim(),
            WithholdingTaxId = withholdingTax?.Id,
            WithholdingTaxName = withholdingTax?.Name,
            WithholdingPercentage = withholdingTax?.Percentage ?? InvoiceTotalsCalculator.RoundMonetary(command.WithholdingPercentage, 2),
            SubtotalNet = calculated.SubtotalNet.Amount,
            TotalVat = calculated.TotalVat.Amount,
            TotalSurcharge = calculated.TotalSurcharge.Amount,
            TotalTax = calculated.TotalTax.Amount,
            TotalGross = calculated.TotalGross.Amount,
            TotalWithholding = calculated.TotalWithholding.Amount,
            TotalPayable = calculated.TotalPayable.Amount,
            Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
            Lines = calculated.Lines.Select(l => new QuoteLine
            {
                Id = Guid.NewGuid(),
                LineOrder = l.LineOrder,
                Description = l.Description,
                Quantity = l.Quantity,
                UnitPriceNet = l.UnitPriceNet,
                VatTaxId = l.VatTaxId,
                VatTaxName = l.VatTaxName,
                VatPercentage = l.VatPercentage,
                SurchargeTaxId = l.SurchargeTaxId,
                SurchargeTaxName = l.SurchargeTaxName,
                SurchargePercentage = l.SurchargePercentage,
                LineNet = l.LineNet.Amount
            }).ToList()
        };

        dbContext.Set<Quote>().Add(quote);
        await dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        await auditLogger.LogCriticalActionAsync(
            action: "Facturacion.Quote.Create",
            entityName: nameof(Quote),
            entityId: quote.Id.ToString(),
            summary: $"Presupuesto creado: {quote.FormattedNumber} para '{quote.CustomerName}' ({quote.TotalGross.ToString("0.00", CultureInfo.InvariantCulture)} EUR).",
            cancellationToken: cancellationToken);

        return FacturacionOperationResult.Ok(quote.Id, quote.FormattedNumber);
    }

    public async Task<FacturacionOperationResult> UpdateQuoteAsync(
        SaveQuoteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!command.Id.HasValue)
        {
            return FacturacionOperationResult.Validation("Debe indicar el identificador del presupuesto a editar.");
        }

        var validationError = ValidateCustomerAndLines(
            command.CustomerName,
            command.CustomerTaxId,
            command.CustomerAddress,
            requireFullCustomerFiscalData: false,
            command.Lines,
            out var normalizedTaxId);
        if (validationError is not null)
        {
            return FacturacionOperationResult.Validation(validationError);
        }

        var quote = await ApplyQuoteSandboxIsolation(dbContext.Set<Quote>())
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == command.Id.Value, cancellationToken);
        if (quote is null)
        {
            return FacturacionOperationResult.NotFoundResult("El presupuesto solicitado no existe.");
        }

        if (quote.Status != QuoteStatus.Borrador)
        {
            return FacturacionOperationResult.Validation(
                "Solo se pueden modificar las líneas e importes de presupuestos en estado Borrador.");
        }

        var (calculated, withholdingTax, calcError) = await ResolveAndCalculateTotalsAsync(
            command.Lines,
            command.WithholdingTaxId,
            command.WithholdingPercentage,
            allowNegativeLines: false,
            cancellationToken);
        if (calcError is not null || calculated is null)
        {
            return FacturacionOperationResult.Validation(calcError ?? "No se pudieron calcular los importes del presupuesto.");
        }

        if (command.RowVersion.HasValue)
        {
            dbContext.Entry(quote).Property(x => x.RowVersion).OriginalValue = command.RowVersion.Value;
        }

        quote.IssueDate = command.IssueDate ?? quote.IssueDate;
        quote.ValidUntilDate = command.ValidUntilDate;
        quote.CustomerName = command.CustomerName.Trim();
        quote.CustomerTaxId = normalizedTaxId;
        quote.CustomerAddress = string.IsNullOrWhiteSpace(command.CustomerAddress) ? null : command.CustomerAddress.Trim();
        quote.WithholdingTaxId = withholdingTax?.Id;
        quote.WithholdingTaxName = withholdingTax?.Name;
        quote.WithholdingPercentage = withholdingTax?.Percentage ?? InvoiceTotalsCalculator.RoundMonetary(command.WithholdingPercentage, 2);
        quote.SubtotalNet = calculated.SubtotalNet.Amount;
        quote.TotalVat = calculated.TotalVat.Amount;
        quote.TotalSurcharge = calculated.TotalSurcharge.Amount;
        quote.TotalTax = calculated.TotalTax.Amount;
        quote.TotalGross = calculated.TotalGross.Amount;
        quote.TotalWithholding = calculated.TotalWithholding.Amount;
        quote.TotalPayable = calculated.TotalPayable.Amount;
        quote.Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim();

        dbContext.Set<QuoteLine>().RemoveRange(quote.Lines);
        var updatedQuoteLines = calculated.Lines.Select(l => new QuoteLine
        {
            Id = Guid.NewGuid(),
            QuoteId = quote.Id,
            LineOrder = l.LineOrder,
            Description = l.Description,
            Quantity = l.Quantity,
            UnitPriceNet = l.UnitPriceNet,
            VatTaxId = l.VatTaxId,
            VatTaxName = l.VatTaxName,
            VatPercentage = l.VatPercentage,
            SurchargeTaxId = l.SurchargeTaxId,
            SurchargeTaxName = l.SurchargeTaxName,
            SurchargePercentage = l.SurchargePercentage,
            LineNet = l.LineNet.Amount
        }).ToList();
        quote.Lines = updatedQuoteLines;
        dbContext.Set<QuoteLine>().AddRange(updatedQuoteLines);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return FacturacionOperationResult.Conflict();
        }

        await auditLogger.LogCriticalActionAsync(
            action: "Facturacion.Quote.Update",
            entityName: nameof(Quote),
            entityId: quote.Id.ToString(),
            summary: $"Presupuesto actualizado: {quote.FormattedNumber}.",
            cancellationToken: cancellationToken);

        return FacturacionOperationResult.Ok(quote.Id, quote.FormattedNumber);
    }

    public async Task<FacturacionOperationResult> ChangeQuoteStatusAsync(
        Guid quoteId,
        QuoteStatus newStatus,
        uint rowVersion,
        CancellationToken cancellationToken = default)
    {
        var quote = await ApplyQuoteSandboxIsolation(dbContext.Set<Quote>())
            .FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken);
        if (quote is null)
        {
            return FacturacionOperationResult.NotFoundResult("El presupuesto solicitado no existe.");
        }

        var validTransition = (quote.Status, newStatus) switch
        {
            (QuoteStatus.Borrador, QuoteStatus.Enviado or QuoteStatus.Aceptado or QuoteStatus.Rechazado or QuoteStatus.Caducado) => true,
            (QuoteStatus.Enviado, QuoteStatus.Aceptado or QuoteStatus.Rechazado or QuoteStatus.Caducado) => true,
            _ => false
        };

        if (!validTransition)
        {
            return FacturacionOperationResult.Validation(
                $"No se permite cambiar el estado del presupuesto de '{quote.Status}' a '{newStatus}'.");
        }

        dbContext.Entry(quote).Property(x => x.RowVersion).OriginalValue = rowVersion;
        quote.Status = newStatus;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return FacturacionOperationResult.Conflict();
        }

        await auditLogger.LogCriticalActionAsync(
            action: "Facturacion.Quote.ChangeStatus",
            entityName: nameof(Quote),
            entityId: quote.Id.ToString(),
            summary: $"Estado de presupuesto {quote.FormattedNumber} cambiado a {newStatus}.",
            cancellationToken: cancellationToken);

        return FacturacionOperationResult.Ok(quote.Id, quote.FormattedNumber);
    }

    public async Task<FacturacionOperationResult> ConvertQuoteToDraftInvoiceAsync(
        Guid quoteId,
        CancellationToken cancellationToken = default)
    {
        var quote = await ApplyQuoteSandboxIsolation(dbContext.Set<Quote>())
            .Include(q => q.Lines.OrderBy(l => l.LineOrder))
            .FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken);
        if (quote is null)
        {
            return FacturacionOperationResult.NotFoundResult("El presupuesto solicitado no existe.");
        }

        if (quote.Status != QuoteStatus.Aceptado)
        {
            return FacturacionOperationResult.Validation(
                "Solo se pueden convertir en borrador de factura los presupuestos en estado Aceptado.");
        }

        if (quote.ConvertedInvoiceId.HasValue)
        {
            return FacturacionOperationResult.Validation(
                "Este presupuesto ya fue convertido previamente en un borrador de factura.");
        }

        var draftCommand = new SaveDraftInvoiceCommand(
            Id: null,
            InvoiceType: InvoiceDocumentType.Ordinaria,
            SeriesId: null,
            IssueDate: FiscalTimeZone.GetMadridDate(DateTimeOffset.UtcNow),
            CustomerName: quote.CustomerName,
            CustomerTaxId: quote.CustomerTaxId,
            CustomerAddress: quote.CustomerAddress,
            PaymentMethod: BillingPaymentMethod.Contado,
            Notes: quote.Notes,
            Lines: quote.Lines.Select(l => new DocumentLineInputDto(
                Description: l.Description,
                Quantity: l.Quantity,
                UnitPriceNet: l.UnitPriceNet,
                VatPercentage: l.VatPercentage,
                VatTaxId: l.VatTaxId,
                SurchargeTaxId: l.SurchargeTaxId,
                SurchargePercentage: l.SurchargePercentage)).ToList(),
            WithholdingTaxId: quote.WithholdingTaxId,
            WithholdingPercentage: quote.WithholdingPercentage);

        var draftResult = await SaveDraftInvoiceInternalAsync(
            draftCommand,
            sourceQuoteId: quote.Id,
            requireCustomerFiscalDataOnDraft: false,
            cancellationToken: cancellationToken);
        if (!draftResult.Succeeded || !draftResult.DocumentId.HasValue)
        {
            return draftResult;
        }

        quote.ConvertedInvoiceId = draftResult.DocumentId.Value;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.LogCriticalActionAsync(
            action: "Facturacion.Quote.ConvertToDraft",
            entityName: nameof(Quote),
            entityId: quote.Id.ToString(),
            summary: $"Presupuesto {quote.FormattedNumber} convertido en borrador de factura ({draftResult.DocumentId}).",
            cancellationToken: cancellationToken);

        return draftResult;
    }

    public async Task<IReadOnlyList<InvoiceDto>> GetInvoicesAsync(CancellationToken cancellationToken = default)
    {
        var invoices = await ApplyInvoiceSandboxIsolation(dbContext.Set<Invoice>().AsNoTracking())
            .Include(i => i.Lines.OrderBy(l => l.LineOrder))
            .Include(i => i.TaxBreakdowns.OrderBy(b => b.TaxKind).ThenBy(b => b.TaxPercentage))
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return invoices.Select(MapInvoiceToDto).ToList();
    }

    public async Task<InvoiceDto?> GetInvoiceByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await ApplyInvoiceSandboxIsolation(dbContext.Set<Invoice>().AsNoTracking())
            .Include(i => i.Lines.OrderBy(l => l.LineOrder))
            .Include(i => i.TaxBreakdowns.OrderBy(b => b.TaxKind).ThenBy(b => b.TaxPercentage))
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        return invoice is null ? null : MapInvoiceToDto(invoice);
    }

    public Task<FacturacionOperationResult> SaveDraftInvoiceAsync(
        SaveDraftInvoiceCommand command,
        CancellationToken cancellationToken = default) =>
        SaveDraftInvoiceInternalAsync(
            command,
            sourceQuoteId: null,
            requireCustomerFiscalDataOnDraft: command.InvoiceType == InvoiceDocumentType.Ordinaria,
            cancellationToken: cancellationToken);

    public async Task<FacturacionOperationResult> DeleteDraftInvoiceAsync(
        Guid draftInvoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await ApplyInvoiceSandboxIsolation(dbContext.Set<Invoice>())
            .FirstOrDefaultAsync(i => i.Id == draftInvoiceId, cancellationToken);
        if (invoice is null)
        {
            return FacturacionOperationResult.NotFoundResult("El borrador de factura solicitado no existe.");
        }

        if (invoice.Status != InvoiceStatus.Borrador)
        {
            return FacturacionOperationResult.Validation(
                "No se puede eliminar una factura emitida. Las facturas emitidas son inmutables y solo admiten rectificación.");
        }

        dbContext.Set<Invoice>().Remove(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.LogCriticalActionAsync(
            action: "Facturacion.DraftInvoice.Delete",
            entityName: nameof(Invoice),
            entityId: draftInvoiceId.ToString(),
            summary: $"Borrador de factura eliminado para cliente '{invoice.CustomerName}'.",
            cancellationToken: cancellationToken);

        return FacturacionOperationResult.Ok(draftInvoiceId);
    }

    public async Task<FacturacionOperationResult> IssueDraftInvoiceAsync(
        Guid draftInvoiceId,
        uint rowVersion,
        CancellationToken cancellationToken = default)
    {
        if (currentUserService.IsDemoSandboxUser)
        {
            return FacturacionOperationResult.DemoBlocked();
        }

        var settings = await EnsureSettingsEntityAsync(cancellationToken);
        var issuerError = ValidateIssuerSettingsForEmission(settings);
        if (issuerError is not null)
        {
            return FacturacionOperationResult.Validation(issuerError);
        }

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var invoice = await ApplyInvoiceSandboxIsolation(dbContext.Set<Invoice>())
            .Include(i => i.Lines.OrderBy(l => l.LineOrder))
            .Include(i => i.TaxBreakdowns)
            .FirstOrDefaultAsync(i => i.Id == draftInvoiceId, cancellationToken);

        if (invoice is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return FacturacionOperationResult.NotFoundResult("El borrador de factura solicitado no existe.");
        }

        if (invoice.Status != InvoiceStatus.Borrador)
        {
            await tx.RollbackAsync(cancellationToken);
            return FacturacionOperationResult.Validation("La factura ya fue emitida previamente.");
        }

        dbContext.Entry(invoice).Property(x => x.RowVersion).OriginalValue = rowVersion;

        var requireFullCustomer = invoice.InvoiceType == InvoiceDocumentType.Ordinaria;
        var lineInputs = invoice.Lines.Select(l => new DocumentLineInputDto(
            Description: l.Description,
            Quantity: l.Quantity,
            UnitPriceNet: l.UnitPriceNet,
            VatPercentage: l.VatPercentage,
            VatTaxId: l.VatTaxId,
            SurchargeTaxId: l.SurchargeTaxId,
            SurchargePercentage: l.SurchargePercentage)).ToList();

        var customerValidationError = ValidateCustomerAndLines(
            invoice.CustomerName,
            invoice.CustomerTaxId,
            invoice.CustomerAddress,
            requireFullCustomerFiscalData: requireFullCustomer,
            lineInputs,
            out var normalizedCustomerTaxId);
        if (customerValidationError is not null)
        {
            await tx.RollbackAsync(cancellationToken);
            return FacturacionOperationResult.Validation(customerValidationError);
        }

        var (calculated, withholdingTax, calcError) = await ResolveAndCalculateTotalsAsync(
            lineInputs,
            invoice.WithholdingTaxId,
            invoice.WithholdingPercentage,
            allowNegativeLines: false,
            cancellationToken);
        if (calcError is not null || calculated is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return FacturacionOperationResult.Validation(calcError ?? "Error al calcular los totales de la factura.");
        }

        var utcNow = DateTimeOffset.UtcNow;
        var madridToday = FiscalTimeZone.GetMadridDate(utcNow);
        var fiscalYear = madridToday.Year;

        var expectedKind = invoice.InvoiceType == InvoiceDocumentType.Simplificada
            ? BillingDocumentKind.Simplificada
            : BillingDocumentKind.Ordinaria;

        var seriesDto = await GetSeriesByIdAndKindAsync(invoice.SeriesId, expectedKind, cancellationToken);
        if (seriesDto is null || seriesDto.FiscalYear != fiscalYear)
        {
            seriesDto = await EnsureDefaultSeriesForYearAsync(expectedKind, fiscalYear, cancellationToken);
        }

        var (seqNumber, formattedNumber, allocError) = await AllocateNextSeriesNumberAsync(
            seriesDto.Id,
            expectedKind,
            cancellationToken);
        if (allocError is not null || !seqNumber.HasValue || formattedNumber is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return FacturacionOperationResult.Validation(allocError ?? "No se pudo asignar número correlativo a la factura.");
        }

        // First persist updated lines and tax snapshot while Status == Borrador (before immutability trigger locks children)
        invoice.CustomerTaxId = normalizedCustomerTaxId;
        invoice.SeriesId = seriesDto.Id;
        invoice.SeriesCode = seriesDto.Code;
        invoice.FiscalYear = seriesDto.FiscalYear;
        invoice.IssueDate = madridToday;
        invoice.IssuerTaxName = settings.TaxName;
        invoice.IssuerTaxId = settings.TaxId;
        invoice.IssuerFiscalAddress = settings.FiscalAddress;
        invoice.WithholdingTaxId = withholdingTax?.Id;
        invoice.WithholdingTaxName = withholdingTax?.Name ?? invoice.WithholdingTaxName;
        invoice.WithholdingPercentage = withholdingTax?.Percentage ?? invoice.WithholdingPercentage;
        invoice.SubtotalNet = calculated.SubtotalNet.Amount;
        invoice.TotalVat = calculated.TotalVat.Amount;
        invoice.TotalSurcharge = calculated.TotalSurcharge.Amount;
        invoice.TotalTax = calculated.TotalTax.Amount;
        invoice.TotalGross = calculated.TotalGross.Amount;
        invoice.TotalWithholding = calculated.TotalWithholding.Amount;
        invoice.TotalPayable = calculated.TotalPayable.Amount;

        dbContext.Set<InvoiceLine>().RemoveRange(invoice.Lines);
        dbContext.Set<InvoiceTaxBreakdown>().RemoveRange(invoice.TaxBreakdowns);

        var newLines = MapCalculatedLinesToEntities(invoice.Id, calculated.Lines);
        var newTaxBreakdowns = MapAppliedTaxesToEntities(invoice.Id, calculated.AppliedTaxes);
        invoice.Lines = newLines;
        invoice.TaxBreakdowns = newTaxBreakdowns;
        dbContext.Set<InvoiceLine>().AddRange(newLines);
        dbContext.Set<InvoiceTaxBreakdown>().AddRange(newTaxBreakdowns);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            invoice.SequentialNumber = seqNumber.Value;
            invoice.FormattedNumber = formattedNumber;
            invoice.IssuedAtUtc = utcNow;
            invoice.Status = InvoiceStatus.Emitida;

            var regResult = await fiscalInvoiceRegistry.RegisterIssuedInvoiceAsync(
                BuildFiscalContext(invoice, rectifiedFormattedNumber: null),
                cancellationToken);
            if (!regResult.Succeeded)
            {
                await tx.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return FacturacionOperationResult.Validation(
                    regResult.ErrorMessage ?? "Error en el registro fiscal de la factura.");
            }

            invoice.FiscalRegistryMetadata = regResult.RegistryMetadata;

            await dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return FacturacionOperationResult.Conflict();
        }

        await auditLogger.LogCriticalActionAsync(
            action: "Facturacion.Invoice.Issue",
            entityName: nameof(Invoice),
            entityId: invoice.Id.ToString(),
            summary: $"Factura emitida definitivamente: {invoice.FormattedNumber} ({invoice.TotalGross.ToString("0.00", CultureInfo.InvariantCulture)} EUR).",
            cancellationToken: cancellationToken);

        return FacturacionOperationResult.Ok(invoice.Id, invoice.FormattedNumber);
    }

    public async Task<FacturacionOperationResult> IssueRectifyingInvoiceAsync(
        IssueRectifyingInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUserService.IsDemoSandboxUser)
        {
            return FacturacionOperationResult.DemoBlocked();
        }

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
        {
            return FacturacionOperationResult.Validation(
                "El motivo de rectificación es obligatorio y no puede superar los 500 caracteres.");
        }

        var settings = await EnsureSettingsEntityAsync(cancellationToken);
        var issuerError = ValidateIssuerSettingsForEmission(settings);
        if (issuerError is not null)
        {
            return FacturacionOperationResult.Validation(issuerError);
        }

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Lock the original invoice row (FOR UPDATE) so concurrent rectifications on the same invoice serialize
        var lockedOriginalIds = await dbContext.Database
            .SqlQueryRaw<Guid>(
                """
                SELECT "Id" AS "Value"
                FROM facturacion_facturas
                WHERE "Id" = {0}
                FOR UPDATE
                """,
                request.OriginalInvoiceId)
            .ToListAsync(cancellationToken);

        if (lockedOriginalIds.Count == 0)
        {
            await tx.RollbackAsync(cancellationToken);
            return FacturacionOperationResult.NotFoundResult("La factura original a rectificar no existe.");
        }

        var original = await ApplyInvoiceSandboxIsolation(dbContext.Set<Invoice>().AsNoTracking())
            .Include(i => i.Lines.OrderBy(l => l.LineOrder))
            .FirstOrDefaultAsync(i => i.Id == request.OriginalInvoiceId, cancellationToken);

        if (original is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return FacturacionOperationResult.NotFoundResult("La factura original a rectificar no existe.");
        }

        if (original.Status != InvoiceStatus.Emitida)
        {
            await tx.RollbackAsync(cancellationToken);
            return FacturacionOperationResult.Validation("Solo se pueden rectificar facturas que estén en estado Emitida.");
        }

        if (original.InvoiceType == InvoiceDocumentType.Rectificativa)
        {
            await tx.RollbackAsync(cancellationToken);
            return FacturacionOperationResult.Validation("No se puede emitir una rectificativa sobre otra factura rectificativa.");
        }

        var existingRectifications = await dbContext.Set<Invoice>()
            .AsNoTracking()
            .Where(i => i.RectifiedInvoiceId == original.Id && i.Status == InvoiceStatus.Emitida)
            .ToListAsync(cancellationToken);

        var alreadyRectifiedNet = existingRectifications.Sum(r => -r.SubtotalNet);
        var alreadyFullyRectified = existingRectifications.Any(r => r.RectificationScope == RectificationScope.Total) ||
                                    alreadyRectifiedNet >= original.SubtotalNet;

        if (alreadyFullyRectified)
        {
            await tx.RollbackAsync(cancellationToken);
            return FacturacionOperationResult.Validation(
                "La factura original ya ha sido rectificada en su totalidad y no admite nuevas rectificativas.");
        }

        if (request.Scope == RectificationScope.Total && existingRectifications.Count > 0)
        {
            await tx.RollbackAsync(cancellationToken);
            return FacturacionOperationResult.Validation(
                "La factura original ya tiene rectificativas parciales previas; utilice rectificación por diferencias por el saldo restante.");
        }

        List<DocumentLineInputDto> rectifyingLineInputs;
        if (request.Scope == RectificationScope.Total)
        {
            rectifyingLineInputs = original.Lines.Select(l => new DocumentLineInputDto(
                Description: $"Rectificación total {original.FormattedNumber}: {l.Description}",
                Quantity: -Math.Abs(l.Quantity),
                UnitPriceNet: l.UnitPriceNet,
                VatPercentage: l.VatPercentage,
                VatTaxId: l.VatTaxId,
                SurchargeTaxId: l.SurchargeTaxId,
                SurchargePercentage: l.SurchargePercentage)).ToList();
        }
        else
        {
            if (request.DifferenceLines is null || request.DifferenceLines.Count == 0)
            {
                await tx.RollbackAsync(cancellationToken);
                return FacturacionOperationResult.Validation(
                    "Para una rectificativa por diferencias debe indicar al menos una línea de ajuste.");
            }

            rectifyingLineInputs = request.DifferenceLines.Select(l => new DocumentLineInputDto(
                Description: l.Description,
                Quantity: l.Quantity,
                UnitPriceNet: l.UnitPriceNet,
                VatPercentage: l.VatPercentage,
                VatTaxId: l.VatTaxId,
                SurchargeTaxId: l.SurchargeTaxId,
                SurchargePercentage: l.SurchargePercentage)).ToList();
        }

        var (calculated, withholdingTax, calcError) = await ResolveAndCalculateTotalsAsync(
            rectifyingLineInputs,
            original.WithholdingTaxId,
            original.WithholdingPercentage,
            allowNegativeLines: true,
            cancellationToken);
        if (calcError is not null || calculated is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return FacturacionOperationResult.Validation(calcError ?? "Error al calcular los importes de la factura rectificativa.");
        }

        if (calculated.SubtotalNet.Amount >= 0m)
        {
            await tx.RollbackAsync(cancellationToken);
            return FacturacionOperationResult.Validation(
                "El importe neto de la factura rectificativa debe ser negativo.");
        }

        var remainingRectifiableNet = original.SubtotalNet - alreadyRectifiedNet;
        if (Math.Abs(calculated.SubtotalNet.Amount) > remainingRectifiableNet)
        {
            await tx.RollbackAsync(cancellationToken);
            return FacturacionOperationResult.Validation(
                $"El importe a rectificar ({Math.Abs(calculated.SubtotalNet.Amount).ToString("0.00", CultureInfo.InvariantCulture)} EUR) supera la base pendiente de la factura original ({remainingRectifiableNet.ToString("0.00", CultureInfo.InvariantCulture)} EUR).");
        }

        var utcNow = DateTimeOffset.UtcNow;
        var madridToday = FiscalTimeZone.GetMadridDate(utcNow);
        var fiscalYear = madridToday.Year;

        var seriesDto = request.SeriesId.HasValue
            ? await GetSeriesByIdAndKindAsync(request.SeriesId.Value, BillingDocumentKind.Rectificativa, cancellationToken)
            : await EnsureDefaultSeriesForYearAsync(BillingDocumentKind.Rectificativa, fiscalYear, cancellationToken);

        if (seriesDto is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return FacturacionOperationResult.Validation("La serie seleccionada no es válida para facturas rectificativas.");
        }

        var (seqNumber, formattedNumber, allocError) = await AllocateNextSeriesNumberAsync(
            seriesDto.Id,
            BillingDocumentKind.Rectificativa,
            cancellationToken);
        if (allocError is not null || !seqNumber.HasValue || formattedNumber is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return FacturacionOperationResult.Validation(allocError ?? "No se pudo numerar la factura rectificativa.");
        }

        // Insert as Borrador first so child lines and tax breakdowns pass BEFORE INSERT trigger check,
        // then transition to Emitida inside the same database transaction.
        var rectifyingId = Guid.NewGuid();
        var rectifying = new Invoice
        {
            Id = rectifyingId,
            InvoiceType = InvoiceDocumentType.Rectificativa,
            Status = InvoiceStatus.Borrador,
            SeriesId = seriesDto.Id,
            SeriesCode = seriesDto.Code,
            FiscalYear = seriesDto.FiscalYear,
            SequentialNumber = seqNumber.Value,
            FormattedNumber = formattedNumber,
            IssueDate = madridToday,
            IssuedAtUtc = utcNow,
            IssuerTaxName = settings.TaxName,
            IssuerTaxId = settings.TaxId,
            IssuerFiscalAddress = settings.FiscalAddress,
            CustomerName = original.CustomerName,
            CustomerTaxId = original.CustomerTaxId,
            CustomerAddress = original.CustomerAddress,
            PaymentMethod = request.PaymentMethod,
            WithholdingTaxId = withholdingTax?.Id ?? original.WithholdingTaxId,
            WithholdingTaxName = withholdingTax?.Name ?? original.WithholdingTaxName,
            WithholdingPercentage = original.WithholdingPercentage,
            SubtotalNet = calculated.SubtotalNet.Amount,
            TotalVat = calculated.TotalVat.Amount,
            TotalSurcharge = calculated.TotalSurcharge.Amount,
            TotalTax = calculated.TotalTax.Amount,
            TotalGross = calculated.TotalGross.Amount,
            TotalWithholding = calculated.TotalWithholding.Amount,
            TotalPayable = calculated.TotalPayable.Amount,
            Notes = $"Factura rectificativa de {original.FormattedNumber}. Motivo: {reason}",
            RectifiedInvoiceId = original.Id,
            RectificationScope = request.Scope,
            RectificationReason = reason,
            Lines = MapCalculatedLinesToEntities(rectifyingId, calculated.Lines),
            TaxBreakdowns = MapAppliedTaxesToEntities(rectifyingId, calculated.AppliedTaxes)
        };

        dbContext.Set<Invoice>().Add(rectifying);
        await dbContext.SaveChangesAsync(cancellationToken);

        rectifying.Status = InvoiceStatus.Emitida;
        var regResult = await fiscalInvoiceRegistry.RegisterIssuedInvoiceAsync(
            BuildFiscalContext(rectifying, original.FormattedNumber),
            cancellationToken);
        if (!regResult.Succeeded)
        {
            await tx.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return FacturacionOperationResult.Validation(
                regResult.ErrorMessage ?? "Error en el registro fiscal de la factura rectificativa.");
        }

        rectifying.FiscalRegistryMetadata = regResult.RegistryMetadata;
        await dbContext.SaveChangesAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);

        await auditLogger.LogCriticalActionAsync(
            action: "Facturacion.Invoice.Rectify",
            entityName: nameof(Invoice),
            entityId: rectifying.Id.ToString(),
            summary: $"Factura rectificativa emitida: {rectifying.FormattedNumber} sobre original {original.FormattedNumber} ({request.Scope}, Total={rectifying.TotalGross.ToString("0.00", CultureInfo.InvariantCulture)} EUR).",
            cancellationToken: cancellationToken);

        return FacturacionOperationResult.Ok(rectifying.Id, rectifying.FormattedNumber);
    }

    public async Task<InvoiceEmissionResult> CreateAndIssueAsync(
        IssueInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var draftCommand = new SaveDraftInvoiceCommand(
            Id: null,
            InvoiceType: request.InvoiceType,
            SeriesId: request.SeriesId,
            IssueDate: request.IssueDate,
            CustomerName: request.CustomerName,
            CustomerTaxId: request.CustomerTaxId,
            CustomerAddress: request.CustomerAddress,
            PaymentMethod: request.PaymentMethod,
            Notes: request.Notes,
            Lines: request.Lines.Select(l => new DocumentLineInputDto(
                Description: l.Description,
                Quantity: l.Quantity,
                UnitPriceNet: l.UnitPriceNet,
                VatPercentage: l.VatPercentage,
                VatTaxId: l.VatTaxId,
                SurchargeTaxId: l.SurchargeTaxId,
                SurchargePercentage: l.SurchargePercentage)).ToList(),
            WithholdingTaxId: request.WithholdingTaxId,
            WithholdingPercentage: request.WithholdingPercentage);

        var saveDraft = await SaveDraftInvoiceAsync(draftCommand, cancellationToken);
        if (!saveDraft.Succeeded || !saveDraft.DocumentId.HasValue)
        {
            return InvoiceEmissionResult.Failure(saveDraft.ErrorMessage ?? "Error al validar los datos de la factura.");
        }

        var draft = await GetInvoiceByIdAsync(saveDraft.DocumentId.Value, cancellationToken);
        if (draft is null)
        {
            return InvoiceEmissionResult.Failure("No se pudo recuperar el borrador de factura recién creado.");
        }

        var issueResult = await IssueDraftInvoiceAsync(draft.Id, draft.RowVersion, cancellationToken);
        if (!issueResult.Succeeded)
        {
            await DeleteDraftInvoiceAsync(draft.Id, cancellationToken);
            return InvoiceEmissionResult.Failure(issueResult.ErrorMessage ?? "Error al emitir la factura.");
        }

        var issued = await GetInvoiceByIdAsync(draft.Id, cancellationToken);
        return issued is null
            ? InvoiceEmissionResult.Failure("No se pudo leer la factura emitida.")
            : InvoiceEmissionResult.Success(MapInvoiceToSummary(issued));
    }

    public async Task<InvoiceEmissionResult> IssueRectifyingAsync(
        IssueRectifyingInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await IssueRectifyingInvoiceAsync(request, cancellationToken);
        if (!result.Succeeded || !result.DocumentId.HasValue)
        {
            return InvoiceEmissionResult.Failure(result.ErrorMessage ?? "No se pudo emitir la factura rectificativa.");
        }

        var issued = await GetInvoiceByIdAsync(result.DocumentId.Value, cancellationToken);
        return issued is null
            ? InvoiceEmissionResult.Failure("No se pudo leer la factura rectificativa emitida.")
            : InvoiceEmissionResult.Success(MapInvoiceToSummary(issued));
    }

    public async Task<PdfDocumentResult> DownloadInvoicePdfAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await GetInvoiceByIdAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            return new PdfDocumentResult(false, ErrorMessage: "La factura solicitada no existe.");
        }

        var settings = await GetSettingsAsync(cancellationToken);
        byte[]? logoBytes = null;
        if (settings.LogoPrivateFileId.HasValue)
        {
            var (loadedLogo, logoError) = await TryLoadAndValidateLogoBytesAsync(settings.LogoPrivateFileId.Value, cancellationToken);
            if (logoError is not null)
            {
                return new PdfDocumentResult(false, ErrorMessage: logoError);
            }

            logoBytes = loadedLogo;
        }

        var pdfBytes = pdfInvoiceGenerator.GenerateInvoicePdf(invoice, settings, logoBytes);
        var numberPart = string.IsNullOrWhiteSpace(invoice.FormattedNumber)
            ? $"BORRADOR-{invoice.Id.ToString("N")[..8]}"
            : invoice.FormattedNumber;
        var fileName = $"Factura_{numberPart}.pdf";

        await auditLogger.LogCriticalActionAsync(
            action: "Facturacion.Invoice.DownloadPdf",
            entityName: nameof(Invoice),
            entityId: invoice.Id.ToString(),
            summary: $"Descarga de PDF de factura '{numberPart}'.",
            cancellationToken: cancellationToken);

        return new PdfDocumentResult(true, pdfBytes, fileName);
    }

    public async Task<PdfDocumentResult> DownloadQuotePdfAsync(
        Guid quoteId,
        CancellationToken cancellationToken = default)
    {
        var quote = await GetQuoteByIdAsync(quoteId, cancellationToken);
        if (quote is null)
        {
            return new PdfDocumentResult(false, ErrorMessage: "El presupuesto solicitado no existe.");
        }

        var settings = await GetSettingsAsync(cancellationToken);
        byte[]? logoBytes = null;
        if (settings.LogoPrivateFileId.HasValue)
        {
            var (loadedLogo, logoError) = await TryLoadAndValidateLogoBytesAsync(settings.LogoPrivateFileId.Value, cancellationToken);
            if (logoError is not null)
            {
                return new PdfDocumentResult(false, ErrorMessage: logoError);
            }

            logoBytes = loadedLogo;
        }

        var pdfBytes = pdfInvoiceGenerator.GenerateQuotePdf(quote, settings, logoBytes);
        var fileName = $"Presupuesto_{quote.FormattedNumber}.pdf";

        await auditLogger.LogCriticalActionAsync(
            action: "Facturacion.Quote.DownloadPdf",
            entityName: nameof(Quote),
            entityId: quote.Id.ToString(),
            summary: $"Descarga de PDF de presupuesto '{quote.FormattedNumber}'.",
            cancellationToken: cancellationToken);

        return new PdfDocumentResult(true, pdfBytes, fileName);
    }

    private async Task<FacturacionOperationResult> SaveDraftInvoiceInternalAsync(
        SaveDraftInvoiceCommand command,
        Guid? sourceQuoteId,
        bool requireCustomerFiscalDataOnDraft,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.InvoiceType == InvoiceDocumentType.Rectificativa)
        {
            return FacturacionOperationResult.Validation(
                "Las facturas rectificativas se emiten directamente sobre una factura original mediante IssueRectifyingInvoiceAsync.");
        }

        var validationError = ValidateCustomerAndLines(
            command.CustomerName,
            command.CustomerTaxId,
            command.CustomerAddress,
            requireFullCustomerFiscalData: requireCustomerFiscalDataOnDraft,
            command.Lines,
            out var normalizedTaxId);
        if (validationError is not null)
        {
            return FacturacionOperationResult.Validation(validationError);
        }

        var (calculated, withholdingTax, calcError) = await ResolveAndCalculateTotalsAsync(
            command.Lines,
            command.WithholdingTaxId,
            command.WithholdingPercentage,
            allowNegativeLines: false,
            cancellationToken);
        if (calcError is not null || calculated is null)
        {
            return FacturacionOperationResult.Validation(calcError ?? "No se pudieron calcular los importes del borrador.");
        }

        var settings = await EnsureSettingsEntityAsync(cancellationToken);
        var issueDate = command.IssueDate ?? FiscalTimeZone.GetMadridDate(DateTimeOffset.UtcNow);
        var fiscalYear = issueDate.Year;

        var docKind = command.InvoiceType == InvoiceDocumentType.Simplificada
            ? BillingDocumentKind.Simplificada
            : BillingDocumentKind.Ordinaria;

        var seriesDto = command.SeriesId.HasValue
            ? await GetSeriesByIdAndKindAsync(command.SeriesId.Value, docKind, cancellationToken)
            : await EnsureDefaultSeriesForYearAsync(docKind, fiscalYear, cancellationToken);

        if (seriesDto is null)
        {
            return FacturacionOperationResult.Validation("La serie seleccionada no es válida para este tipo de factura.");
        }

        if (command.Id.HasValue)
        {
            var existing = await ApplyInvoiceSandboxIsolation(dbContext.Set<Invoice>())
                .Include(i => i.Lines)
                .Include(i => i.TaxBreakdowns)
                .FirstOrDefaultAsync(i => i.Id == command.Id.Value, cancellationToken);

            if (existing is null)
            {
                return FacturacionOperationResult.NotFoundResult("El borrador de factura solicitado no existe.");
            }

            if (existing.Status != InvoiceStatus.Borrador)
            {
                return FacturacionOperationResult.Validation(
                    "Una factura emitida es inmutable y no puede editarse. Emita una factura rectificativa.");
            }

            if (command.RowVersion.HasValue)
            {
                dbContext.Entry(existing).Property(x => x.RowVersion).OriginalValue = command.RowVersion.Value;
            }

            existing.InvoiceType = command.InvoiceType;
            existing.SeriesId = seriesDto.Id;
            existing.SeriesCode = seriesDto.Code;
            existing.FiscalYear = seriesDto.FiscalYear;
            existing.IssueDate = issueDate;
            existing.IssuerTaxName = settings.TaxName;
            existing.IssuerTaxId = settings.TaxId;
            existing.IssuerFiscalAddress = settings.FiscalAddress;
            existing.CustomerName = command.CustomerName.Trim();
            existing.CustomerTaxId = normalizedTaxId;
            existing.CustomerAddress = string.IsNullOrWhiteSpace(command.CustomerAddress) ? null : command.CustomerAddress.Trim();
            existing.PaymentMethod = command.PaymentMethod;
            existing.WithholdingTaxId = withholdingTax?.Id;
            existing.WithholdingTaxName = withholdingTax?.Name;
            existing.WithholdingPercentage = withholdingTax?.Percentage ?? InvoiceTotalsCalculator.RoundMonetary(command.WithholdingPercentage, 2);
            existing.SubtotalNet = calculated.SubtotalNet.Amount;
            existing.TotalVat = calculated.TotalVat.Amount;
            existing.TotalSurcharge = calculated.TotalSurcharge.Amount;
            existing.TotalTax = calculated.TotalTax.Amount;
            existing.TotalGross = calculated.TotalGross.Amount;
            existing.TotalWithholding = calculated.TotalWithholding.Amount;
            existing.TotalPayable = calculated.TotalPayable.Amount;
            existing.Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim();

            dbContext.Set<InvoiceLine>().RemoveRange(existing.Lines);
            dbContext.Set<InvoiceTaxBreakdown>().RemoveRange(existing.TaxBreakdowns);

            var updatedLines = MapCalculatedLinesToEntities(existing.Id, calculated.Lines);
            var updatedTaxes = MapAppliedTaxesToEntities(existing.Id, calculated.AppliedTaxes);
            existing.Lines = updatedLines;
            existing.TaxBreakdowns = updatedTaxes;
            dbContext.Set<InvoiceLine>().AddRange(updatedLines);
            dbContext.Set<InvoiceTaxBreakdown>().AddRange(updatedTaxes);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                dbContext.ChangeTracker.Clear();
                return FacturacionOperationResult.Conflict();
            }

            await auditLogger.LogCriticalActionAsync(
                action: "Facturacion.DraftInvoice.Update",
                entityName: nameof(Invoice),
                entityId: existing.Id.ToString(),
                summary: $"Borrador de factura actualizado para '{existing.CustomerName}'.",
                cancellationToken: cancellationToken);

            return FacturacionOperationResult.Ok(existing.Id);
        }

        var draftId = Guid.NewGuid();
        var draft = new Invoice
        {
            Id = draftId,
            InvoiceType = command.InvoiceType,
            Status = InvoiceStatus.Borrador,
            SeriesId = seriesDto.Id,
            SeriesCode = seriesDto.Code,
            FiscalYear = seriesDto.FiscalYear,
            SequentialNumber = null,
            FormattedNumber = null,
            IssueDate = issueDate,
            IssuedAtUtc = null,
            IssuerTaxName = settings.TaxName,
            IssuerTaxId = settings.TaxId,
            IssuerFiscalAddress = settings.FiscalAddress,
            CustomerName = command.CustomerName.Trim(),
            CustomerTaxId = normalizedTaxId,
            CustomerAddress = string.IsNullOrWhiteSpace(command.CustomerAddress) ? null : command.CustomerAddress.Trim(),
            PaymentMethod = command.PaymentMethod,
            WithholdingTaxId = withholdingTax?.Id,
            WithholdingTaxName = withholdingTax?.Name,
            WithholdingPercentage = withholdingTax?.Percentage ?? InvoiceTotalsCalculator.RoundMonetary(command.WithholdingPercentage, 2),
            SubtotalNet = calculated.SubtotalNet.Amount,
            TotalVat = calculated.TotalVat.Amount,
            TotalSurcharge = calculated.TotalSurcharge.Amount,
            TotalTax = calculated.TotalTax.Amount,
            TotalGross = calculated.TotalGross.Amount,
            TotalWithholding = calculated.TotalWithholding.Amount,
            TotalPayable = calculated.TotalPayable.Amount,
            Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
            SourceQuoteId = sourceQuoteId,
            Lines = MapCalculatedLinesToEntities(draftId, calculated.Lines),
            TaxBreakdowns = MapAppliedTaxesToEntities(draftId, calculated.AppliedTaxes)
        };

        dbContext.Set<Invoice>().Add(draft);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.LogCriticalActionAsync(
            action: "Facturacion.DraftInvoice.Create",
            entityName: nameof(Invoice),
            entityId: draft.Id.ToString(),
            summary: $"Borrador de factura creado para '{draft.CustomerName}' ({draft.TotalGross.ToString("0.00", CultureInfo.InvariantCulture)} EUR).",
            cancellationToken: cancellationToken);

        return FacturacionOperationResult.Ok(draft.Id);
    }

    private async Task<(int? SequentialNumber, string? FormattedNumber, string? ErrorMessage)> AllocateNextSeriesNumberAsync(
        Guid seriesId,
        BillingDocumentKind expectedKind,
        CancellationToken cancellationToken)
    {
        var series = await dbContext.Set<BillingSeries>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.DocumentKind == expectedKind && s.IsActive, cancellationToken);

        if (series is null)
        {
            return (null, null, "La serie de numeración indicada no existe, está inactiva o no corresponde al tipo de documento.");
        }

        var now = DateTimeOffset.UtcNow;
        var actor = currentUserService.GetActorIdentifier();

        // Atomic row-level lock & increment in PostgreSQL
        var allocatedNumbers = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                UPDATE facturacion_series
                SET "LastNumber" = "LastNumber" + 1,
                    "UpdatedAtUtc" = {0},
                    "UpdatedBy" = {1}
                WHERE "Id" = {2} AND "IsActive" = TRUE
                RETURNING "LastNumber" AS "Value"
                """,
                now,
                actor,
                seriesId)
            .ToListAsync(cancellationToken);

        if (allocatedNumbers.Count == 0)
        {
            return (null, null, "No se pudo incrementar el contador correlativo de la serie seleccionada.");
        }

        var nextNumber = allocatedNumbers[0];
        var formatted = series.FormatDocumentNumber(nextNumber);
        return (nextNumber, formatted, null);
    }

    private async Task<(CalculatedDocumentTotals? Totals, TaxRateDefinition? WithholdingTax, string? ErrorMessage)> ResolveAndCalculateTotalsAsync(
        IReadOnlyList<DocumentLineInputDto> lines,
        Guid? withholdingTaxId,
        decimal withholdingPercentage,
        bool allowNegativeLines,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultTaxCatalogAsync(cancellationToken);
        var allTaxes = await dbContext.Set<TaxRateDefinition>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var byId = allTaxes.ToDictionary(t => t.Id);
        var taxedLines = new List<TaxedLineInput>(lines.Count);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.Description) || line.Description.Trim().Length > 300)
            {
                return (null, null, "La descripción de cada línea es obligatoria y no puede superar 300 caracteres.");
            }

            if ((!allowNegativeLines && line.Quantity <= 0m) || (allowNegativeLines && line.Quantity == 0m))
            {
                return (null, null, "La cantidad de cada línea debe ser distinta de cero (y positiva en documentos ordinarios).");
            }

            if (line.UnitPriceNet < 0m)
            {
                return (null, null, "El precio unitario neto no puede ser negativo.");
            }

            TaxRateDefinition? vatTax = null;
            if (line.VatTaxId.HasValue)
            {
                if (!byId.TryGetValue(line.VatTaxId.Value, out vatTax) || vatTax.Kind != TaxKind.Iva)
                {
                    return (null, null, "El tipo de IVA seleccionado en una de las líneas no existe.");
                }
            }
            else
            {
                var roundedVat = InvoiceTotalsCalculator.RoundMonetary(line.VatPercentage, 2);
                vatTax = allTaxes
                    .Where(t => t.Kind == TaxKind.Iva && t.Percentage == roundedVat)
                    .OrderByDescending(t => t.IsActive)
                    .ThenByDescending(t => t.IsDefault)
                    .FirstOrDefault();
            }

            var effectiveVatPct = vatTax?.Percentage ?? InvoiceTotalsCalculator.RoundMonetary(line.VatPercentage, 2);
            var effectiveVatName = vatTax?.Name ?? $"IVA {effectiveVatPct.ToString("0.##", CultureInfo.InvariantCulture)}%";

            TaxRateDefinition? surchargeTax = null;
            var effectiveSurchargePct = InvoiceTotalsCalculator.RoundMonetary(line.SurchargePercentage, 2);
            string? effectiveSurchargeName = null;

            if (line.SurchargeTaxId.HasValue)
            {
                if (!byId.TryGetValue(line.SurchargeTaxId.Value, out surchargeTax) ||
                    surchargeTax.Kind != TaxKind.RecargoEquivalencia)
                {
                    return (null, null, "El tipo de Recargo de Equivalencia seleccionado no es válido.");
                }

                if (vatTax is not null && surchargeTax.ParentVatTaxId.HasValue && surchargeTax.ParentVatTaxId.Value != vatTax.Id)
                {
                    return (null, null, $"El recargo '{surchargeTax.Name}' no corresponde al tipo de IVA '{vatTax.Name}'.");
                }

                effectiveSurchargePct = surchargeTax.Percentage;
                effectiveSurchargeName = surchargeTax.Name;
            }
            else if (effectiveSurchargePct > 0m)
            {
                surchargeTax = allTaxes
                    .Where(t => t.Kind == TaxKind.RecargoEquivalencia &&
                                t.Percentage == effectiveSurchargePct &&
                                (vatTax is null || t.ParentVatTaxId == vatTax.Id))
                    .OrderByDescending(t => t.IsActive)
                    .FirstOrDefault();
                effectiveSurchargeName = surchargeTax?.Name ?? $"R.E. {effectiveSurchargePct.ToString("0.##", CultureInfo.InvariantCulture)}%";
            }

            taxedLines.Add(new TaxedLineInput(
                Description: line.Description.Trim(),
                Quantity: line.Quantity,
                UnitPriceNet: line.UnitPriceNet,
                VatPercentage: effectiveVatPct,
                VatTaxId: vatTax?.Id,
                VatTaxName: effectiveVatName,
                SurchargeTaxId: surchargeTax?.Id,
                SurchargeTaxName: effectiveSurchargeName,
                SurchargePercentage: effectiveSurchargePct,
                AeatTaxCode: vatTax?.AeatTaxCode ?? "01",
                AeatRegimeKey: effectiveSurchargePct > 0m ? "18" : (vatTax?.AeatRegimeKey ?? "01"),
                AeatOperationQualification: vatTax?.AeatOperationQualification ?? (effectiveVatPct == 0m ? "E1" : "S1")));
        }

        TaxRateDefinition? withholdingTax = null;
        WithholdingTaxInput? withholdingInput = null;
        var roundedIrpf = InvoiceTotalsCalculator.RoundMonetary(withholdingPercentage, 2);

        if (withholdingTaxId.HasValue)
        {
            if (!byId.TryGetValue(withholdingTaxId.Value, out withholdingTax) || withholdingTax.Kind != TaxKind.Irpf)
            {
                return (null, null, "El tipo de retención IRPF seleccionado no es válido.");
            }

            withholdingInput = new WithholdingTaxInput(withholdingTax.Percentage, withholdingTax.Id, withholdingTax.Name);
        }
        else if (roundedIrpf > 0m)
        {
            withholdingTax = allTaxes
                .Where(t => t.Kind == TaxKind.Irpf && t.Percentage == roundedIrpf)
                .OrderByDescending(t => t.IsActive)
                .ThenByDescending(t => t.IsDefault)
                .FirstOrDefault();

            withholdingInput = new WithholdingTaxInput(
                roundedIrpf,
                withholdingTax?.Id,
                withholdingTax?.Name ?? $"Retención IRPF {roundedIrpf.ToString("0.##", CultureInfo.InvariantCulture)}%");
        }

        var calculated = InvoiceTotalsCalculator.Calculate(taxedLines, withholdingInput);
        return (calculated, withholdingTax, null);
    }

    private static string? ValidateCustomerAndLines(
        string? customerName,
        string? customerTaxId,
        string? customerAddress,
        bool requireFullCustomerFiscalData,
        IReadOnlyList<DocumentLineInputDto>? lines,
        out string? normalizedTaxId)
    {
        normalizedTaxId = null;

        if (string.IsNullOrWhiteSpace(customerName) || customerName.Trim().Length > 200)
        {
            return "El nombre o razón social del cliente es obligatorio y no puede superar los 200 caracteres.";
        }

        if (requireFullCustomerFiscalData)
        {
            if (string.IsNullOrWhiteSpace(customerTaxId))
            {
                return "El NIF/CIF/NIE del cliente es obligatorio para facturas ordinarias.";
            }

            if (string.IsNullOrWhiteSpace(customerAddress) || customerAddress.Trim().Length > 500)
            {
                return "La dirección fiscal del cliente es obligatoria para facturas ordinarias (máximo 500 caracteres).";
            }
        }

        if (!string.IsNullOrWhiteSpace(customerTaxId))
        {
            if (!SpanishTaxIdValidator.IsValid(customerTaxId, out var taxIdError))
            {
                return taxIdError ?? "El NIF/CIF/NIE del cliente no es válido.";
            }

            normalizedTaxId = SpanishTaxIdValidator.Normalize(customerTaxId);
        }

        if (lines is null || lines.Count == 0)
        {
            return "El documento debe incluir al menos una línea de detalle.";
        }

        return null;
    }

    private static string? ValidateIssuerSettingsForEmission(BillingSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.TaxName))
        {
            return "No se puede emitir la factura: falta configurar el nombre o razón social del emisor en Ajustes de Facturación.";
        }

        if (string.IsNullOrWhiteSpace(settings.FiscalAddress))
        {
            return "No se puede emitir la factura: falta configurar el domicilio fiscal del emisor en Ajustes de Facturación.";
        }

        if (!SpanishTaxIdValidator.IsValid(settings.TaxId, out var issuerTaxError))
        {
            return $"No se puede emitir la factura: el NIF/CIF/NIE del emisor no está configurado o es inválido ({issuerTaxError}).";
        }

        return null;
    }

    private async Task<BillingSettings> EnsureSettingsEntityAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.Set<BillingSettings>()
            .OrderBy(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        settings = new BillingSettings
        {
            Id = Guid.NewGuid(),
            TaxName = string.Empty,
            TaxId = string.Empty,
            FiscalAddress = string.Empty,
            LegalTermsText = "Factura sujeta a la normativa fiscal española vigente.",
            FooterText = "Gracias por su confianza."
        };

        dbContext.Set<BillingSettings>().Add(settings);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(settings).State = EntityState.Detached;
            return await dbContext.Set<BillingSettings>().OrderBy(s => s.CreatedAtUtc).FirstAsync(cancellationToken);
        }

        return settings;
    }

    private async Task EnsureDefaultTaxCatalogAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Set<TaxRateDefinition>().AnyAsync(cancellationToken))
        {
            return;
        }

        var iva21 = new TaxRateDefinition
        {
            Id = Guid.NewGuid(),
            Code = "IVA-21",
            Name = "IVA General 21%",
            Kind = TaxKind.Iva,
            Percentage = 21.00m,
            Applicability = TaxApplicability.Ambos,
            AeatTaxCode = "01",
            AeatRegimeKey = "01",
            AeatOperationQualification = "S1",
            IsActive = true,
            IsDefault = true
        };

        var iva10 = new TaxRateDefinition
        {
            Id = Guid.NewGuid(),
            Code = "IVA-10",
            Name = "IVA Reducido 10%",
            Kind = TaxKind.Iva,
            Percentage = 10.00m,
            Applicability = TaxApplicability.Ambos,
            AeatTaxCode = "01",
            AeatRegimeKey = "01",
            AeatOperationQualification = "S1",
            IsActive = true,
            IsDefault = false
        };

        var iva04 = new TaxRateDefinition
        {
            Id = Guid.NewGuid(),
            Code = "IVA-04",
            Name = "IVA Superreducido 4%",
            Kind = TaxKind.Iva,
            Percentage = 4.00m,
            Applicability = TaxApplicability.Ambos,
            AeatTaxCode = "01",
            AeatRegimeKey = "01",
            AeatOperationQualification = "S1",
            IsActive = true,
            IsDefault = false
        };

        var iva00 = new TaxRateDefinition
        {
            Id = Guid.NewGuid(),
            Code = "IVA-00",
            Name = "IVA Exento 0%",
            Kind = TaxKind.Iva,
            Percentage = 0.00m,
            Applicability = TaxApplicability.Ambos,
            AeatTaxCode = "01",
            AeatRegimeKey = "01",
            AeatOperationQualification = "E1",
            IsActive = true,
            IsDefault = false
        };

        var re52 = new TaxRateDefinition
        {
            Id = Guid.NewGuid(),
            Code = "RE-52",
            Name = "Recargo de Equivalencia 5,2%",
            Kind = TaxKind.RecargoEquivalencia,
            Percentage = 5.20m,
            ParentVatTaxId = iva21.Id,
            Applicability = TaxApplicability.B2B,
            AeatTaxCode = "01",
            AeatRegimeKey = "18",
            AeatOperationQualification = "S1",
            IsActive = true,
            IsDefault = false
        };

        var re14 = new TaxRateDefinition
        {
            Id = Guid.NewGuid(),
            Code = "RE-14",
            Name = "Recargo de Equivalencia 1,4%",
            Kind = TaxKind.RecargoEquivalencia,
            Percentage = 1.40m,
            ParentVatTaxId = iva10.Id,
            Applicability = TaxApplicability.B2B,
            AeatTaxCode = "01",
            AeatRegimeKey = "18",
            AeatOperationQualification = "S1",
            IsActive = true,
            IsDefault = false
        };

        var re05 = new TaxRateDefinition
        {
            Id = Guid.NewGuid(),
            Code = "RE-05",
            Name = "Recargo de Equivalencia 0,5%",
            Kind = TaxKind.RecargoEquivalencia,
            Percentage = 0.50m,
            ParentVatTaxId = iva04.Id,
            Applicability = TaxApplicability.B2B,
            AeatTaxCode = "01",
            AeatRegimeKey = "18",
            AeatOperationQualification = "S1",
            IsActive = true,
            IsDefault = false
        };

        var irpf15 = new TaxRateDefinition
        {
            Id = Guid.NewGuid(),
            Code = "IRPF-15",
            Name = "Retención IRPF Profesional 15%",
            Kind = TaxKind.Irpf,
            Percentage = 15.00m,
            Applicability = TaxApplicability.B2B,
            AeatTaxCode = "IRPF",
            AeatRegimeKey = "01",
            AeatOperationQualification = "NA",
            IsActive = true,
            IsDefault = true
        };

        var irpf07 = new TaxRateDefinition
        {
            Id = Guid.NewGuid(),
            Code = "IRPF-07",
            Name = "Retención IRPF Inicio Actividad 7%",
            Kind = TaxKind.Irpf,
            Percentage = 7.00m,
            Applicability = TaxApplicability.B2B,
            AeatTaxCode = "IRPF",
            AeatRegimeKey = "01",
            AeatOperationQualification = "NA",
            IsActive = true,
            IsDefault = false
        };

        var irpf19 = new TaxRateDefinition
        {
            Id = Guid.NewGuid(),
            Code = "IRPF-19",
            Name = "Retención IRPF Arrendamiento 19%",
            Kind = TaxKind.Irpf,
            Percentage = 19.00m,
            Applicability = TaxApplicability.B2B,
            AeatTaxCode = "IRPF",
            AeatRegimeKey = "11",
            AeatOperationQualification = "NA",
            IsActive = true,
            IsDefault = false
        };

        dbContext.Set<TaxRateDefinition>().AddRange(
            iva21, iva10, iva04, iva00,
            re52, re14, re05,
            irpf15, irpf07, irpf19);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            foreach (var entry in dbContext.ChangeTracker.Entries<TaxRateDefinition>().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    private async Task<BillingSeriesDto?> GetSeriesByIdAndKindAsync(
        Guid seriesId,
        BillingDocumentKind expectedKind,
        CancellationToken cancellationToken)
    {
        var series = await ApplySeriesSandboxIsolation(dbContext.Set<BillingSeries>().AsNoTracking())
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.DocumentKind == expectedKind && s.IsActive, cancellationToken);

        return series is null ? null : MapSeriesToDto(series);
    }

    private async Task<(byte[]? Bytes, string? ErrorMessage)> TryLoadAndValidateLogoBytesAsync(
        Guid logoPrivateFileId,
        CancellationToken cancellationToken)
    {
        var hexId = logoPrivateFileId.ToString("N");
        string[] candidateNames = [$"{hexId}.jpg", $"{hexId}.png", $"{hexId}.webp", $"{hexId}.pdf"];

        foreach (var candidate in candidateNames)
        {
            var opened = await privateFileStorage.OpenReadAsync(candidate, cancellationToken);
            if (opened is null)
            {
                continue;
            }

            await using var stream = opened.Value.Stream;
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            var bytes = ms.ToArray();

            try
            {
                PdfInvoiceGenerator.EnsureValidLogoFormat(bytes);
                return (bytes, null);
            }
            catch (InvalidOperationException ex)
            {
                return (null, ex.Message);
            }
        }

        return (null, "El archivo de logotipo indicado no existe en el almacenamiento privado.");
    }

    private IQueryable<BillingSeries> ApplySeriesSandboxIsolation(IQueryable<BillingSeries> query)
    {
        if (currentUserService.IsDemoSandboxUser)
        {
            var demoUserId = currentUserService.GetActorIdentifier();
            return query.Where(s => s.CreatedBy == demoUserId);
        }

        return query.Where(s => !s.CreatedBy.StartsWith(DemoSandboxConstants.DemoUserIdPrefix));
    }

    private IQueryable<Quote> ApplyQuoteSandboxIsolation(IQueryable<Quote> query)
    {
        if (currentUserService.IsDemoSandboxUser)
        {
            var demoUserId = currentUserService.GetActorIdentifier();
            return query.Where(q => q.CreatedBy == demoUserId);
        }

        return query.Where(q => !q.CreatedBy.StartsWith(DemoSandboxConstants.DemoUserIdPrefix));
    }

    private IQueryable<Invoice> ApplyInvoiceSandboxIsolation(IQueryable<Invoice> query)
    {
        if (currentUserService.IsDemoSandboxUser)
        {
            var demoUserId = currentUserService.GetActorIdentifier();
            return query.Where(i => i.CreatedBy == demoUserId);
        }

        return query.Where(i => !i.CreatedBy.StartsWith(DemoSandboxConstants.DemoUserIdPrefix));
    }

    private string GetDemoSeriesSuffix()
    {
        var actor = currentUserService.GetActorIdentifier();
        var clean = new string(actor.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return clean.Length <= 8 ? clean : clean[^8..];
    }

    private static List<InvoiceLine> MapCalculatedLinesToEntities(
        Guid invoiceId,
        IReadOnlyList<CalculatedDocumentLine> lines) =>
        lines.Select(l => new InvoiceLine
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            LineOrder = l.LineOrder,
            Description = l.Description,
            Quantity = l.Quantity,
            UnitPriceNet = l.UnitPriceNet,
            VatTaxId = l.VatTaxId,
            VatTaxName = l.VatTaxName,
            VatPercentage = l.VatPercentage,
            SurchargeTaxId = l.SurchargeTaxId,
            SurchargeTaxName = l.SurchargeTaxName,
            SurchargePercentage = l.SurchargePercentage,
            LineNet = l.LineNet.Amount
        }).ToList();

    private static List<InvoiceTaxBreakdown> MapAppliedTaxesToEntities(
        Guid invoiceId,
        IReadOnlyList<AppliedTaxSnapshotItem> appliedTaxes) =>
        appliedTaxes.Select(t => new InvoiceTaxBreakdown
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            TaxId = t.TaxId,
            TaxKind = t.TaxKind,
            TaxName = t.TaxName,
            TaxPercentage = t.TaxPercentage,
            LinkedVatPercentage = t.LinkedVatPercentage,
            AeatTaxCode = t.AeatTaxCode,
            AeatRegimeKey = t.AeatRegimeKey,
            AeatOperationQualification = t.AeatOperationQualification,
            TaxableBase = t.TaxableBase.Amount,
            TaxAmount = t.TaxAmount.Amount
        }).ToList();

    private static BillingSettingsDto MapSettingsToDto(BillingSettings s)
    {
        var isReady = !string.IsNullOrWhiteSpace(s.TaxName) &&
                      !string.IsNullOrWhiteSpace(s.FiscalAddress) &&
                      SpanishTaxIdValidator.IsValid(s.TaxId, out _);

        return new BillingSettingsDto(
            s.Id,
            s.TaxName,
            s.TaxId,
            s.FiscalAddress,
            s.LogoPrivateFileId,
            s.LegalTermsText,
            s.FooterText,
            s.RowVersion,
            isReady);
    }

    private static BillingSeriesDto MapSeriesToDto(BillingSeries s) =>
        new(
            s.Id,
            s.Code,
            s.Name,
            s.DocumentKind,
            s.FiscalYear,
            s.LastNumber,
            s.PaddingDigits,
            s.IsActive,
            s.IsDefault,
            s.RowVersion);

    private static QuoteDto MapQuoteToDto(Quote q)
    {
        var calc = InvoiceTotalsCalculator.Calculate(
            q.Lines.OrderBy(l => l.LineOrder).Select(l => new TaxedLineInput(
                Description: l.Description,
                Quantity: l.Quantity,
                UnitPriceNet: l.UnitPriceNet,
                VatPercentage: l.VatPercentage,
                VatTaxId: l.VatTaxId,
                VatTaxName: l.VatTaxName,
                SurchargeTaxId: l.SurchargeTaxId,
                SurchargeTaxName: l.SurchargeTaxName,
                SurchargePercentage: l.SurchargePercentage)),
            q.WithholdingPercentage > 0m
                ? new WithholdingTaxInput(q.WithholdingPercentage, q.WithholdingTaxId, q.WithholdingTaxName)
                : null);

        return new QuoteDto(
            q.Id,
            q.SeriesId,
            q.SeriesCode,
            q.FiscalYear,
            q.SequentialNumber,
            q.FormattedNumber,
            q.Status,
            q.IssueDate,
            q.ValidUntilDate,
            q.CustomerName,
            q.CustomerTaxId,
            q.CustomerAddress,
            q.WithholdingTaxId,
            q.WithholdingTaxName,
            q.WithholdingPercentage,
            q.SubtotalNet,
            q.TotalVat,
            q.TotalSurcharge,
            q.TotalTax,
            q.TotalGross,
            q.TotalWithholding,
            q.TotalPayable,
            q.Notes,
            q.ConvertedInvoiceId,
            q.RowVersion,
            q.Lines.OrderBy(l => l.LineOrder).Select(l => new DocumentLineDto(
                l.Id,
                l.LineOrder,
                l.Description,
                l.Quantity,
                l.UnitPriceNet,
                l.VatTaxId,
                l.VatTaxName,
                l.VatPercentage,
                l.SurchargeTaxId,
                l.SurchargeTaxName,
                l.SurchargePercentage,
                l.LineNet)).ToList(),
            calc.VatBreakdown.Select(b => new VatBreakdownDto(
                b.VatPercentage,
                b.VatTaxName,
                b.TaxableBase.Amount,
                b.TaxAmount.Amount,
                b.SurchargePercentage,
                b.SurchargeTaxName,
                b.SurchargeAmount.Amount,
                b.GrossAmount.Amount)).ToList());
    }

    private static InvoiceDto MapInvoiceToDto(Invoice i)
    {
        var calc = InvoiceTotalsCalculator.Calculate(
            i.Lines.OrderBy(l => l.LineOrder).Select(l => new TaxedLineInput(
                Description: l.Description,
                Quantity: l.Quantity,
                UnitPriceNet: l.UnitPriceNet,
                VatPercentage: l.VatPercentage,
                VatTaxId: l.VatTaxId,
                VatTaxName: l.VatTaxName,
                SurchargeTaxId: l.SurchargeTaxId,
                SurchargeTaxName: l.SurchargeTaxName,
                SurchargePercentage: l.SurchargePercentage)),
            i.WithholdingPercentage > 0m
                ? new WithholdingTaxInput(i.WithholdingPercentage, i.WithholdingTaxId, i.WithholdingTaxName)
                : null);

        return new InvoiceDto(
            i.Id,
            i.InvoiceType,
            i.Status,
            i.SeriesId,
            i.SeriesCode,
            i.FiscalYear,
            i.SequentialNumber,
            i.FormattedNumber,
            i.IssueDate,
            i.IssuedAtUtc,
            i.IssuerTaxName,
            i.IssuerTaxId,
            i.IssuerFiscalAddress,
            i.CustomerName,
            i.CustomerTaxId,
            i.CustomerAddress,
            i.PaymentMethod,
            i.WithholdingTaxId,
            i.WithholdingTaxName,
            i.WithholdingPercentage,
            i.SubtotalNet,
            i.TotalVat,
            i.TotalSurcharge,
            i.TotalTax,
            i.TotalGross,
            i.TotalWithholding,
            i.TotalPayable,
            i.Notes,
            i.SourceQuoteId,
            i.RectifiedInvoiceId,
            i.RectificationScope,
            i.RectificationReason,
            i.RowVersion,
            i.Lines.OrderBy(l => l.LineOrder).Select(l => new DocumentLineDto(
                l.Id,
                l.LineOrder,
                l.Description,
                l.Quantity,
                l.UnitPriceNet,
                l.VatTaxId,
                l.VatTaxName,
                l.VatPercentage,
                l.SurchargeTaxId,
                l.SurchargeTaxName,
                l.SurchargePercentage,
                l.LineNet)).ToList(),
            calc.VatBreakdown.Select(b => new VatBreakdownDto(
                b.VatPercentage,
                b.VatTaxName,
                b.TaxableBase.Amount,
                b.TaxAmount.Amount,
                b.SurchargePercentage,
                b.SurchargeTaxName,
                b.SurchargeAmount.Amount,
                b.GrossAmount.Amount)).ToList(),
            i.TaxBreakdowns.OrderBy(b => b.TaxKind).ThenBy(b => b.TaxPercentage).Select(b => new InvoiceTaxBreakdownDto(
                b.Id,
                b.TaxId,
                b.TaxKind,
                b.TaxName,
                b.TaxPercentage,
                b.LinkedVatPercentage,
                b.AeatTaxCode,
                b.AeatRegimeKey,
                b.AeatOperationQualification,
                b.TaxableBase,
                b.TaxAmount)).ToList());
    }

    private static IssuedInvoiceSummary MapInvoiceToSummary(InvoiceDto i) =>
        new(
            i.Id,
            i.InvoiceType,
            i.SeriesCode,
            i.FiscalYear,
            i.SequentialNumber ?? 0,
            i.FormattedNumber ?? string.Empty,
            i.IssuedAtUtc ?? DateTimeOffset.UtcNow,
            i.SubtotalNet,
            i.TotalVat,
            i.TotalSurcharge,
            i.TotalTax,
            i.TotalGross,
            i.TotalWithholding,
            i.TotalPayable);

    private static IssuedInvoiceFiscalContext BuildFiscalContext(Invoice invoice, string? rectifiedFormattedNumber) =>
        new(
            InvoiceId: invoice.Id,
            InvoiceType: invoice.InvoiceType,
            SeriesCode: invoice.SeriesCode,
            FiscalYear: invoice.FiscalYear,
            SequentialNumber: invoice.SequentialNumber ?? 0,
            FormattedNumber: invoice.FormattedNumber ?? string.Empty,
            IssuedAtUtc: invoice.IssuedAtUtc ?? DateTimeOffset.UtcNow,
            IssueDate: invoice.IssueDate,
            IssuerTaxId: invoice.IssuerTaxId,
            IssuerTaxName: invoice.IssuerTaxName,
            CustomerName: invoice.CustomerName,
            CustomerTaxId: invoice.CustomerTaxId,
            SubtotalNet: invoice.SubtotalNet,
            TotalVat: invoice.TotalVat,
            TotalSurcharge: invoice.TotalSurcharge,
            TotalTax: invoice.TotalTax,
            TotalGross: invoice.TotalGross,
            TotalWithholding: invoice.TotalWithholding,
            TotalPayable: invoice.TotalPayable,
            RectifiedInvoiceId: invoice.RectifiedInvoiceId,
            RectifiedFormattedNumber: rectifiedFormattedNumber,
            TaxBreakdown: invoice.TaxBreakdowns.Select(b => new FiscalTaxBreakdownEntry(
                TaxKind: b.TaxKind,
                TaxName: b.TaxName,
                TaxPercentage: b.TaxPercentage,
                LinkedVatPercentage: b.LinkedVatPercentage,
                AeatTaxCode: b.AeatTaxCode,
                AeatRegimeKey: b.AeatRegimeKey,
                AeatOperationQualification: b.AeatOperationQualification,
                TaxableBase: b.TaxableBase,
                TaxAmount: b.TaxAmount)).ToList());
}
