using Paye.Core.Entities;

namespace Paye.Core.Modules.Notas;

/// <summary>
/// Entidad del módulo autocontenido de ejemplo Notas.
/// Hereda auditoría automática (CreatedAt/By, UpdatedAt/By), token de concurrencia PostgreSQL (xmin RowVersion)
/// e implementa borrado lógico (ISoftDeletable).
/// </summary>
public sealed class Nota : AuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Titulo { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public bool Archivada { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
