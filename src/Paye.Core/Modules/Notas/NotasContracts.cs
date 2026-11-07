namespace Paye.Core.Modules.Notas;

public sealed record NotaFilterQuery(
    string? SearchTerm = null,
    bool? Archivada = null,
    int Page = 1,
    int PageSize = 10);

public sealed record NotaDto(
    Guid Id,
    string Titulo,
    string Contenido,
    bool Archivada,
    DateTimeOffset CreatedAtUtc,
    string CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy,
    uint RowVersion);

public sealed record NotasPagedResult(
    IReadOnlyList<NotaDto> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
}

public sealed record CreateNotaCommand(
    string Titulo,
    string Contenido,
    bool Archivada);

public sealed record UpdateNotaCommand(
    Guid Id,
    string Titulo,
    string Contenido,
    bool Archivada,
    uint RowVersion);

public enum NotasOperationStatus
{
    Success,
    NotFound,
    ConcurrencyConflict,
    ValidationError
}

public sealed record NotasOperationResult(
    NotasOperationStatus Status,
    Guid? NotaId = null,
    string? ErrorMessage = null)
{
    public bool Succeeded => Status == NotasOperationStatus.Success;
}

public interface INotasService
{
    Task<NotasPagedResult> GetPagedAsync(
        NotaFilterQuery query,
        CancellationToken cancellationToken = default);

    Task<NotaDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<NotasOperationResult> CreateAsync(
        CreateNotaCommand command,
        CancellationToken cancellationToken = default);

    Task<NotasOperationResult> UpdateAsync(
        UpdateNotaCommand command,
        CancellationToken cancellationToken = default);

    Task<NotasOperationResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
