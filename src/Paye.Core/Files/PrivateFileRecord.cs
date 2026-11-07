using Paye.Core.Entities;

namespace Paye.Core.Files;

/// <summary>
/// Metadatos persistidos de un archivo privado subido fuera de wwwroot.
/// Hereda de AuditableEntity (CreatedAt/By, UpdatedAt/By, xmin RowVersion) e implementa borrado lógico (ISoftDeletable).
/// </summary>
public sealed class PrivateFileRecord : AuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StoredFileName { get; set; } = string.Empty;
    public string OriginalDisplayName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Description { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
