using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Paye.Core.Modules.Clientes;

namespace Paye.Infrastructure.Modules.Clientes;

public sealed class ClienteEntityConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("clientes");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.NombreFiscal)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.NombreComercial)
            .HasMaxLength(150);

        builder.Property(c => c.CifNif)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Email)
            .HasMaxLength(100);

        builder.Property(c => c.Telefono)
            .HasMaxLength(30);

        builder.Property(c => c.Direccion)
            .HasMaxLength(200);

        builder.Property(c => c.CodigoPostal)
            .HasMaxLength(10);

        builder.Property(c => c.Ciudad)
            .HasMaxLength(100);

        builder.Property(c => c.Provincia)
            .HasMaxLength(100);

        builder.Property(c => c.Activo)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(c => c.Notas)
            .HasMaxLength(1000);

        builder.Property(c => c.CreatedBy)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(c => c.UpdatedBy)
            .HasMaxLength(128);

        builder.Property(c => c.DeletedBy)
            .HasMaxLength(128);

        builder.HasIndex(c => c.NombreFiscal);
        builder.HasIndex(c => c.CifNif);
        builder.HasIndex(c => c.CreatedAtUtc);
    }
}
