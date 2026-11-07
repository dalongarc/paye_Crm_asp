using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Paye.Core.Demo;
using Paye.Core.Invoicing;
using Paye.Core.Modules.Facturacion;
using Paye.Infrastructure.Data;

namespace Paye.Infrastructure.Modules.Facturacion;

public sealed class FacturacionDemoDataProvider(
    PayeDbContext dbContext) : IDemoModuleDataProvider
{
    public IReadOnlyList<string> GetDemoPermissions() =>
    [
        FacturacionPermissions.Ver,
        FacturacionPermissions.Crear,
        FacturacionPermissions.Descargar
    ];

    public async Task SeedDemoDataAsync(
        string demoUserId,
        CancellationToken cancellationToken = default)
    {
        if (!DemoSandboxConstants.IsDemoUserId(demoUserId))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var madridToday = FiscalTimeZone.GetMadridDate(now);
        var fiscalYear = madridToday.Year;
        var cleanSuffix = new string(demoUserId.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (cleanSuffix.Length > 8)
        {
            cleanSuffix = cleanSuffix[^8..];
        }

        var quoteSeries = new BillingSeries
        {
            Id = Guid.NewGuid(),
            Code = $"PD{cleanSuffix}",
            Name = $"Presupuestos Demo {fiscalYear.ToString(CultureInfo.InvariantCulture)}",
            DocumentKind = BillingDocumentKind.Presupuesto,
            FiscalYear = fiscalYear,
            LastNumber = 1,
            PaddingDigits = 4,
            IsActive = true,
            IsDefault = true,
            CreatedAtUtc = now,
            CreatedBy = demoUserId
        };

        var draftSeries = new BillingSeries
        {
            Id = Guid.NewGuid(),
            Code = $"FD{cleanSuffix}",
            Name = $"Facturas Ordinarias Demo {fiscalYear.ToString(CultureInfo.InvariantCulture)}",
            DocumentKind = BillingDocumentKind.Ordinaria,
            FiscalYear = fiscalYear,
            LastNumber = 0,
            PaddingDigits = 4,
            IsActive = true,
            IsDefault = true,
            CreatedAtUtc = now,
            CreatedBy = demoUserId
        };

        var quoteId = Guid.NewGuid();
        var sampleQuote = new Quote
        {
            Id = quoteId,
            SeriesId = quoteSeries.Id,
            SeriesCode = quoteSeries.Code,
            FiscalYear = fiscalYear,
            SequentialNumber = 1,
            FormattedNumber = quoteSeries.FormatDocumentNumber(1),
            Status = QuoteStatus.Borrador,
            IssueDate = madridToday,
            ValidUntilDate = madridToday.AddDays(15),
            CustomerName = "Cafetería Central S.L. (Demo)",
            CustomerTaxId = "B12345674",
            CustomerAddress = "Calle Mayor 10, 28013 Madrid",
            SubtotalNet = 100.00m,
            TotalVat = 21.00m,
            TotalSurcharge = 0.00m,
            TotalTax = 21.00m,
            TotalGross = 121.00m,
            TotalWithholding = 0.00m,
            TotalPayable = 121.00m,
            Notes = "Presupuesto de demostración aislado en su sesión. Al cerrar sesión se eliminará automáticamente.",
            CreatedAtUtc = now,
            CreatedBy = demoUserId,
            Lines =
            [
                new QuoteLine
                {
                    Id = Guid.NewGuid(),
                    QuoteId = quoteId,
                    LineOrder = 1,
                    Description = "Servicio de consultoría e implantación inicial (Demo)",
                    Quantity = 1m,
                    UnitPriceNet = 100.00m,
                    VatTaxName = "IVA General 21%",
                    VatPercentage = 21.00m,
                    SurchargePercentage = 0.00m,
                    LineNet = 100.00m
                }
            ]
        };

        dbContext.Set<BillingSeries>().AddRange(quoteSeries, draftSeries);
        dbContext.Set<Quote>().Add(sampleQuote);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CleanupDemoDataAsync(
        string demoUserId,
        CancellationToken cancellationToken = default)
    {
        if (!DemoSandboxConstants.IsDemoUserId(demoUserId))
        {
            return;
        }

        await dbContext.Set<Invoice>()
            .IgnoreQueryFilters()
            .Where(i => i.CreatedBy == demoUserId && i.Status == InvoiceStatus.Borrador)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.Set<Quote>()
            .IgnoreQueryFilters()
            .Where(q => q.CreatedBy == demoUserId)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.Set<BillingSeries>()
            .IgnoreQueryFilters()
            .Where(s => s.CreatedBy == demoUserId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
