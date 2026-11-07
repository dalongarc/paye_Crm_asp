using Paye.Core.Entities;

namespace Paye.Core.Modules.Clientes;

/// <summary>
/// Entidad del módulo Clientes.
/// Hereda auditoría automática (CreatedAt/By, UpdatedAt/By), token de concurrencia PostgreSQL (xmin RowVersion)
/// e implementa borrado lógico (ISoftDeletable).
/// </summary>
public sealed class Cliente : AuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string NombreFiscal { get; set; } = string.Empty;
    public string NombreComercial { get; set; } = string.Empty;
    public string CifNif { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string CodigoPostal { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;
    public string Provincia { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public string? Notas { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
