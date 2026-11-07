using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Paye.Core.Modules.Notas;

namespace Paye.Infrastructure.Modules.Notas;

public sealed class NotaEntityConfiguration : IEntityTypeConfiguration<Nota>
{
    public void Configure(EntityTypeBuilder<Nota> builder)
    {
        builder.ToTable("notas");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Titulo)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(n => n.Contenido)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(n => n.Archivada)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(n => n.CreatedBy)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(n => n.UpdatedBy)
            .HasMaxLength(128);

        builder.Property(n => n.DeletedBy)
            .HasMaxLength(128);

        builder.HasIndex(n => n.CreatedAtUtc);
        builder.HasIndex(n => n.Archivada);
    }
}
