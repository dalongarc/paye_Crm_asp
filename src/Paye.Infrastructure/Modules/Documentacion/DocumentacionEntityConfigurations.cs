using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Paye.Core.Modules.Clientes;
using Paye.Core.Modules.Documentacion;

namespace Paye.Infrastructure.Modules.Documentacion;

public sealed class DocumentacionCompanySettingsEntityConfiguration : IEntityTypeConfiguration<DocumentacionCompanySettings>
{
    public void Configure(EntityTypeBuilder<DocumentacionCompanySettings> builder)
    {
        builder.ToTable("documentacion_ajustes_empresa");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.NombreEmpresa).HasMaxLength(200).IsRequired();
        builder.Property(s => s.CifNif).HasMaxLength(30).IsRequired();
        builder.Property(s => s.Direccion).HasMaxLength(300).IsRequired();
        builder.Property(s => s.Telefono).HasMaxLength(40).IsRequired();
        builder.Property(s => s.Email).HasMaxLength(150).IsRequired();
        builder.Property(s => s.Web).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Iban).HasMaxLength(50).IsRequired();
        builder.Property(s => s.LogoUrl).HasMaxLength(500).IsRequired();
        builder.Property(s => s.TextoLegalTitulo).HasMaxLength(150).IsRequired();
        builder.Property(s => s.TextoLegalContenido).HasMaxLength(2000).IsRequired();

        builder.Property(s => s.CreatedBy).HasMaxLength(128).IsRequired();
        builder.Property(s => s.UpdatedBy).HasMaxLength(128);
    }
}

public sealed class DocumentoSerieEntityConfiguration : IEntityTypeConfiguration<DocumentoSerie>
{
    public void Configure(EntityTypeBuilder<DocumentoSerie> builder)
    {
        builder.ToTable("documentacion_series");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Tipo).IsRequired();
        builder.Property(s => s.Codigo).HasMaxLength(12).IsRequired();
        builder.Property(s => s.FiscalYear).IsRequired();
        builder.Property(s => s.LastNumber).IsRequired();
        builder.Property(s => s.PaddingDigits).HasDefaultValue(3).IsRequired();

        builder.Property(s => s.CreatedBy).HasMaxLength(128).IsRequired();
        builder.Property(s => s.UpdatedBy).HasMaxLength(128);

        builder.HasIndex(s => new { s.Tipo, s.FiscalYear }).IsUnique();
    }
}

public sealed class DocumentoComercialEntityConfiguration : IEntityTypeConfiguration<DocumentoComercial>
{
    public void Configure(EntityTypeBuilder<DocumentoComercial> builder)
    {
        builder.ToTable("documentacion_documentos");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Tipo).IsRequired();
        builder.Property(d => d.FiscalYear).IsRequired();
        builder.Property(d => d.SequentialNumber).IsRequired();
        builder.Property(d => d.NumeroDocumento).HasMaxLength(32).IsRequired();

        builder.Property(d => d.Titulo).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Subtitulo).HasMaxLength(250).IsRequired();
        builder.Property(d => d.Categoria).HasMaxLength(120).IsRequired();

        builder.Property(d => d.ClienteId).IsRequired(false);
        builder.Property(d => d.ClienteNombre).HasMaxLength(200).IsRequired();
        builder.Property(d => d.ClienteCifNif).HasMaxLength(30).IsRequired();
        builder.Property(d => d.ClienteDireccion).HasMaxLength(300).IsRequired();
        builder.Property(d => d.ClienteEmail).HasMaxLength(150).IsRequired();
        builder.Property(d => d.ClienteTelefono).HasMaxLength(40).IsRequired();

        builder.Property(d => d.FechaEmision).IsRequired();
        builder.Property(d => d.FechaValidez);

        builder.Property(d => d.ObjetoContrato).HasMaxLength(500);
        builder.Property(d => d.DuracionContrato).HasMaxLength(200);
        builder.Property(d => d.PrecioTextoContrato).HasMaxLength(300);
        builder.Property(d => d.FormaPagoContrato).HasMaxLength(300);
        builder.Property(d => d.LugarFirma).HasMaxLength(150);

        builder.Property(d => d.BloquesJson).HasColumnType("jsonb").IsRequired();

        builder.Property(d => d.SubtotalBruto).HasPrecision(18, 2).IsRequired();
        builder.Property(d => d.TotalDescuento).HasPrecision(18, 2).IsRequired();
        builder.Property(d => d.BaseImponible).HasPrecision(18, 2).IsRequired();
        builder.Property(d => d.TotalIva).HasPrecision(18, 2).IsRequired();
        builder.Property(d => d.TotalGeneral).HasPrecision(18, 2).IsRequired();

        builder.Property(d => d.CreatedBy).HasMaxLength(128).IsRequired();
        builder.Property(d => d.UpdatedBy).HasMaxLength(128);
        builder.Property(d => d.DeletedBy).HasMaxLength(128);

        builder.HasOne<Cliente>()
            .WithMany()
            .HasForeignKey(d => d.ClienteId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.Lineas)
            .WithOne()
            .HasForeignKey(l => l.DocumentoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.NumeroDocumento).IsUnique();
        builder.HasIndex(d => new { d.Tipo, d.FiscalYear, d.SequentialNumber }).IsUnique();
        builder.HasIndex(d => d.ClienteId);
        builder.HasIndex(d => d.Tipo);
        builder.HasIndex(d => d.FechaEmision);
    }
}

public sealed class DocumentoLineaEntityConfiguration : IEntityTypeConfiguration<DocumentoLinea>
{
    public void Configure(EntityTypeBuilder<DocumentoLinea> builder)
    {
        builder.ToTable("documentacion_lineas");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Orden).IsRequired();
        builder.Property(l => l.Descripcion).HasMaxLength(500).IsRequired();
        builder.Property(l => l.Cantidad).HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.PrecioUnitario).HasPrecision(18, 2).IsRequired();
        builder.Property(l => l.DescuentoPorcentaje).HasPrecision(5, 2).IsRequired();
        builder.Property(l => l.IvaPorcentaje).HasPrecision(5, 2).IsRequired();

        builder.Property(l => l.ImporteBruto).HasPrecision(18, 2).IsRequired();
        builder.Property(l => l.ImporteDescuento).HasPrecision(18, 2).IsRequired();
        builder.Property(l => l.BaseImponible).HasPrecision(18, 2).IsRequired();
        builder.Property(l => l.CuotaIva).HasPrecision(18, 2).IsRequired();
        builder.Property(l => l.TotalLinea).HasPrecision(18, 2).IsRequired();

        builder.HasIndex(l => new { l.DocumentoId, l.Orden });
    }
}
