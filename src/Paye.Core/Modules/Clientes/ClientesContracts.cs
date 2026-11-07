using System.ComponentModel.DataAnnotations;

namespace Paye.Core.Modules.Clientes;

public sealed record ClienteDto(
    Guid Id,
    string NombreFiscal,
    string NombreComercial,
    string CifNif,
    string Email,
    string Telefono,
    string Direccion,
    string CodigoPostal,
    string Ciudad,
    string Provincia,
    bool Activo,
    string? Notas,
    uint RowVersion,
    DateTimeOffset CreatedAtUtc,
    string CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy);

public sealed record ClienteFilterQuery
{
    public string? SearchTerm { get; init; }
    public bool? Activo { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public sealed record ClientesPagedResult(
    IReadOnlyList<ClienteDto> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public sealed record CreateClienteRequest
{
    [Required(ErrorMessage = "El nombre fiscal es obligatorio.")]
    [MaxLength(150, ErrorMessage = "El nombre fiscal no puede superar 150 caracteres.")]
    public string NombreFiscal { get; init; } = string.Empty;

    [MaxLength(150, ErrorMessage = "El nombre comercial no puede superar 150 caracteres.")]
    public string? NombreComercial { get; init; }

    [Required(ErrorMessage = "El CIF/NIF es obligatorio.")]
    [MaxLength(20, ErrorMessage = "El CIF/NIF no puede superar 20 caracteres.")]
    public string CifNif { get; init; } = string.Empty;

    [EmailAddress(ErrorMessage = "El formato de correo electrónico no es válido.")]
    [MaxLength(100, ErrorMessage = "El correo electrónico no puede superar 100 caracteres.")]
    public string? Email { get; init; }

    [MaxLength(30, ErrorMessage = "El teléfono no puede superar 30 caracteres.")]
    public string? Telefono { get; init; }

    [MaxLength(200, ErrorMessage = "La dirección no puede superar 200 caracteres.")]
    public string? Direccion { get; init; }

    [MaxLength(10, ErrorMessage = "El código postal no puede superar 10 caracteres.")]
    public string? CodigoPostal { get; init; }

    [MaxLength(100, ErrorMessage = "La ciudad no puede superar 100 caracteres.")]
    public string? Ciudad { get; init; }

    [MaxLength(100, ErrorMessage = "La provincia no puede superar 100 caracteres.")]
    public string? Provincia { get; init; }

    public bool Activo { get; init; } = true;

    [MaxLength(1000, ErrorMessage = "Las notas no pueden superar 1000 caracteres.")]
    public string? Notas { get; init; }
}

public sealed record UpdateClienteRequest
{
    [Required(ErrorMessage = "El nombre fiscal es obligatorio.")]
    [MaxLength(150, ErrorMessage = "El nombre fiscal no puede superar 150 caracteres.")]
    public string NombreFiscal { get; init; } = string.Empty;

    [MaxLength(150, ErrorMessage = "El nombre comercial no puede superar 150 caracteres.")]
    public string? NombreComercial { get; init; }

    [Required(ErrorMessage = "El CIF/NIF es obligatorio.")]
    [MaxLength(20, ErrorMessage = "El CIF/NIF no puede superar 20 caracteres.")]
    public string CifNif { get; init; } = string.Empty;

    [EmailAddress(ErrorMessage = "El formato de correo electrónico no es válido.")]
    [MaxLength(100, ErrorMessage = "El correo electrónico no puede superar 100 caracteres.")]
    public string? Email { get; init; }

    [MaxLength(30, ErrorMessage = "El teléfono no puede superar 30 caracteres.")]
    public string? Telefono { get; init; }

    [MaxLength(200, ErrorMessage = "La dirección no puede superar 200 caracteres.")]
    public string? Direccion { get; init; }

    [MaxLength(10, ErrorMessage = "El código postal no puede superar 10 caracteres.")]
    public string? CodigoPostal { get; init; }

    [MaxLength(100, ErrorMessage = "La ciudad no puede superar 100 caracteres.")]
    public string? Ciudad { get; init; }

    [MaxLength(100, ErrorMessage = "La provincia no puede superar 100 caracteres.")]
    public string? Provincia { get; init; }

    public bool Activo { get; init; } = true;

    [MaxLength(1000, ErrorMessage = "Las notas no pueden superar 1000 caracteres.")]
    public string? Notas { get; init; }

    [Required(ErrorMessage = "El token de concurrencia RowVersion es obligatorio.")]
    public uint RowVersion { get; init; }
}

public enum ClientesOperationStatus
{
    Success,
    NotFound,
    ValidationError,
    ConcurrencyConflict
}

public sealed record ClientesOperationResult(
    ClientesOperationStatus Status,
    Guid? ClienteId = null,
    string? ErrorMessage = null,
    ClienteDto? CurrentCliente = null);

public interface IClientesService
{
    Task<ClientesPagedResult> GetPagedAsync(ClienteFilterQuery query, CancellationToken cancellationToken = default);
    Task<ClienteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ClientesOperationResult> CreateAsync(CreateClienteRequest request, CancellationToken cancellationToken = default);
    Task<ClientesOperationResult> UpdateAsync(Guid id, UpdateClienteRequest request, CancellationToken cancellationToken = default);
    Task<ClientesOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
