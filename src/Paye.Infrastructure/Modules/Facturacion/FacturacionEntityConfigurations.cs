using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Paye.Core.Modules.Facturacion;

namespace Paye.Infrastructure.Modules.Facturacion;

public sealed class BillingSettingsEntityConfiguration : IEntityTypeConfiguration<BillingSettings>
{
    public void Configure(EntityTypeBuilder<BillingSettings> builder)
    {
        builder.ToTable("facturacion_config");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.TaxName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.TaxId)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.FiscalAddress)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.LegalTermsText)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.FooterText)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.UpdatedBy)
            .HasMaxLength(128);
    }
}

public sealed class TaxRateDefinitionEntityConfiguration : IEntityTypeConfiguration<TaxRateDefinition>
{
    public void Configure(EntityTypeBuilder<TaxRateDefinition> builder)
    {
        builder.ToTable("facturacion_impuestos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Code)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.Kind)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Percentage)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.Applicability)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.AeatTaxCode)
            .HasMaxLength(4)
            .IsRequired();

        builder.Property(x => x.AeatRegimeKey)
            .HasMaxLength(4)
            .IsRequired();

        builder.Property(x => x.AeatOperationQualification)
            .HasMaxLength(4)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.IsDefault)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.UpdatedBy)
            .HasMaxLength(128);

        builder.HasOne<TaxRateDefinition>()
            .WithMany()
            .HasForeignKey(x => x.ParentVatTaxId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.HasIndex(x => new { x.Kind, x.IsActive, x.Applicability });
        builder.HasIndex(x => x.ParentVatTaxId);
    }
}

public sealed class BillingSeriesEntityConfiguration : IEntityTypeConfiguration<BillingSeries>
{
    public void Configure(EntityTypeBuilder<BillingSeries> builder)
    {
        builder.ToTable("facturacion_series");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Code)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.DocumentKind)
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(x => x.FiscalYear)
            .IsRequired();

        builder.Property(x => x.LastNumber)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.PaddingDigits)
            .HasDefaultValue(4)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.IsDefault)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.UpdatedBy)
            .HasMaxLength(128);

        builder.HasIndex(x => new { x.Code, x.FiscalYear })
            .IsUnique();

        builder.HasIndex(x => new { x.DocumentKind, x.FiscalYear, x.IsActive });
    }
}

public sealed class QuoteEntityConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("facturacion_presupuestos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.SeriesCode)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.FormattedNumber)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(x => x.CustomerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CustomerTaxId)
            .HasMaxLength(32);

        builder.Property(x => x.CustomerAddress)
            .HasMaxLength(500);

        builder.Property(x => x.WithholdingTaxName)
            .HasMaxLength(120);

        builder.Property(x => x.WithholdingPercentage)
            .HasPrecision(5, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.SubtotalNet)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.TotalVat)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.TotalSurcharge)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.TotalTax)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.TotalGross)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.TotalWithholding)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.TotalPayable)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.UpdatedBy)
            .HasMaxLength(128);

        builder.Property(x => x.DeletedBy)
            .HasMaxLength(128);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SeriesId, x.FiscalYear, x.SequentialNumber })
            .IsUnique();

        builder.HasIndex(x => x.FormattedNumber)
            .IsUnique();

        builder.HasIndex(x => x.CreatedAtUtc);
    }
}

public sealed class QuoteLineEntityConfiguration : IEntityTypeConfiguration<QuoteLine>
{
    public void Configure(EntityTypeBuilder<QuoteLine> builder)
    {
        builder.ToTable("facturacion_presupuesto_lineas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Description)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.Quantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.UnitPriceNet)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.VatTaxName)
            .HasMaxLength(120)
            .HasDefaultValue("IVA 21%")
            .IsRequired();

        builder.Property(x => x.VatPercentage)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.SurchargeTaxName)
            .HasMaxLength(120);

        builder.Property(x => x.SurchargePercentage)
            .HasPrecision(5, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.LineNet)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(x => new { x.QuoteId, x.LineOrder });
    }
}

public sealed class InvoiceEntityConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("facturacion_facturas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.InvoiceType)
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(x => x.SeriesCode)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.FormattedNumber)
            .HasMaxLength(40);

        builder.Property(x => x.IssuerTaxName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.IssuerTaxId)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.IssuerFiscalAddress)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.CustomerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CustomerTaxId)
            .HasMaxLength(32);

        builder.Property(x => x.CustomerAddress)
            .HasMaxLength(500);

        builder.Property(x => x.PaymentMethod)
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(x => x.WithholdingTaxName)
            .HasMaxLength(120);

        builder.Property(x => x.WithholdingPercentage)
            .HasPrecision(5, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.SubtotalNet)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.TotalVat)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.TotalSurcharge)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.TotalTax)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.TotalGross)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.TotalWithholding)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.TotalPayable)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        builder.Property(x => x.RectificationScope)
            .HasConversion<string>()
            .HasMaxLength(24);

        builder.Property(x => x.RectificationReason)
            .HasMaxLength(500);

        builder.Property(x => x.FiscalRegistryMetadata)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.UpdatedBy)
            .HasMaxLength(128);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.TaxBreakdowns)
            .WithOne()
            .HasForeignKey(b => b.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Invoice>()
            .WithMany()
            .HasForeignKey(x => x.RectifiedInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.SeriesId, x.FiscalYear, x.SequentialNumber })
            .IsUnique();

        builder.HasIndex(x => x.FormattedNumber)
            .IsUnique();

        builder.HasIndex(x => x.RectifiedInvoiceId);
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}

public sealed class InvoiceLineEntityConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("facturacion_factura_lineas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Description)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.Quantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.UnitPriceNet)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.VatTaxName)
            .HasMaxLength(120)
            .HasDefaultValue("IVA 21%")
            .IsRequired();

        builder.Property(x => x.VatPercentage)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.SurchargeTaxName)
            .HasMaxLength(120);

        builder.Property(x => x.SurchargePercentage)
            .HasPrecision(5, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.LineNet)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(x => new { x.InvoiceId, x.LineOrder });
    }
}

public sealed class InvoiceTaxBreakdownEntityConfiguration : IEntityTypeConfiguration<InvoiceTaxBreakdown>
{
    public void Configure(EntityTypeBuilder<InvoiceTaxBreakdown> builder)
    {
        builder.ToTable("facturacion_factura_impuestos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.TaxKind)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.TaxName)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.TaxPercentage)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.LinkedVatPercentage)
            .HasPrecision(5, 2);

        builder.Property(x => x.AeatTaxCode)
            .HasMaxLength(4)
            .IsRequired();

        builder.Property(x => x.AeatRegimeKey)
            .HasMaxLength(4)
            .IsRequired();

        builder.Property(x => x.AeatOperationQualification)
            .HasMaxLength(4)
            .IsRequired();

        builder.Property(x => x.TaxableBase)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.TaxAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(x => new { x.InvoiceId, x.TaxKind, x.TaxPercentage });
    }
}
