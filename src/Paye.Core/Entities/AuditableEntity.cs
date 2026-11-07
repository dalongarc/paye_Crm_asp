namespace Paye.Core.Entities;

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAtUtc { get; set; }
    string? DeletedBy { get; set; }
}

public interface IConcurrencyTracked
{
    uint RowVersion { get; set; }
}

public abstract class AuditableEntity : IConcurrencyTracked
{
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Mapeado a la columna de sistema xmin de PostgreSQL para concurrencia optimista.
    /// </summary>
    public uint RowVersion { get; set; }
}
