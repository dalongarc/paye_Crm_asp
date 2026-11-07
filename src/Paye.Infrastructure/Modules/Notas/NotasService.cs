using Microsoft.EntityFrameworkCore;
using Paye.Core.Auditing;
using Paye.Core.Common;
using Paye.Core.Demo;
using Paye.Core.Modules.Notas;
using Paye.Infrastructure.Data;

namespace Paye.Infrastructure.Modules.Notas;

public sealed class NotasService(
    PayeDbContext dbContext,
    ICurrentUserService currentUserService,
    IAuditLogger auditLogger) : INotasService
{
    public async Task<NotasPagedResult> GetPagedAsync(
        NotaFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);

        IQueryable<Nota> notasQuery = ApplySandboxIsolation(dbContext.Set<Nota>().AsNoTracking());

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            notasQuery = notasQuery.Where(n =>
                n.Titulo.Contains(term) || n.Contenido.Contains(term));
        }

        if (query.Archivada.HasValue)
        {
            var archivada = query.Archivada.Value;
            notasQuery = notasQuery.Where(n => n.Archivada == archivada);
        }

        var totalCount = await notasQuery.CountAsync(cancellationToken);

        var items = await notasQuery
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotaDto(
                n.Id,
                n.Titulo,
                n.Contenido,
                n.Archivada,
                n.CreatedAtUtc,
                n.CreatedBy,
                n.UpdatedAtUtc,
                n.UpdatedBy,
                n.RowVersion))
            .ToListAsync(cancellationToken);

        return new NotasPagedResult(items, totalCount, page, pageSize);
    }

    public async Task<NotaDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await ApplySandboxIsolation(dbContext.Set<Nota>().AsNoTracking())
            .Where(n => n.Id == id)
            .Select(n => new NotaDto(
                n.Id,
                n.Titulo,
                n.Contenido,
                n.Archivada,
                n.CreatedAtUtc,
                n.CreatedBy,
                n.UpdatedAtUtc,
                n.UpdatedBy,
                n.RowVersion))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<NotasOperationResult> CreateAsync(
        CreateNotaCommand command,
        CancellationToken cancellationToken = default)
    {
        var titulo = command.Titulo?.Trim() ?? string.Empty;
        var contenido = command.Contenido?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(titulo) || titulo.Length > 150)
        {
            return new NotasOperationResult(
                NotasOperationStatus.ValidationError,
                ErrorMessage: "El título es obligatorio y no puede superar los 150 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(contenido) || contenido.Length > 4000)
        {
            return new NotasOperationResult(
                NotasOperationStatus.ValidationError,
                ErrorMessage: "El contenido es obligatorio y no puede superar los 4000 caracteres.");
        }

        var nota = new Nota
        {
            Id = Guid.NewGuid(),
            Titulo = titulo,
            Contenido = contenido,
            Archivada = command.Archivada
        };

        dbContext.Set<Nota>().Add(nota);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.LogCriticalActionAsync(
            action: "Notas.Create",
            entityName: nameof(Nota),
            entityId: nota.Id.ToString(),
            summary: $"Nota creada: '{nota.Titulo}'.",
            cancellationToken: cancellationToken);

        return new NotasOperationResult(NotasOperationStatus.Success, NotaId: nota.Id);
    }

    public async Task<NotasOperationResult> UpdateAsync(
        UpdateNotaCommand command,
        CancellationToken cancellationToken = default)
    {
        var titulo = command.Titulo?.Trim() ?? string.Empty;
        var contenido = command.Contenido?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(titulo) || titulo.Length > 150)
        {
            return new NotasOperationResult(
                NotasOperationStatus.ValidationError,
                ErrorMessage: "El título es obligatorio y no puede superar los 150 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(contenido) || contenido.Length > 4000)
        {
            return new NotasOperationResult(
                NotasOperationStatus.ValidationError,
                ErrorMessage: "El contenido es obligatorio y no puede superar los 4000 caracteres.");
        }

        var nota = await ApplySandboxIsolation(dbContext.Set<Nota>())
            .FirstOrDefaultAsync(n => n.Id == command.Id, cancellationToken);

        if (nota is null)
        {
            return new NotasOperationResult(
                NotasOperationStatus.NotFound,
                ErrorMessage: "La nota solicitada no existe o fue eliminada.");
        }

        // Optimistic concurrency check via PostgreSQL xmin RowVersion
        dbContext.Entry(nota).Property(n => n.RowVersion).OriginalValue = command.RowVersion;

        nota.Titulo = titulo;
        nota.Contenido = contenido;
        nota.Archivada = command.Archivada;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new NotasOperationResult(
                NotasOperationStatus.ConcurrencyConflict,
                NotaId: nota.Id,
                ErrorMessage: "La nota fue modificada por otro usuario mientras la editaba. Recargue la página para ver los cambios recientes.");
        }

        await auditLogger.LogCriticalActionAsync(
            action: "Notas.Update",
            entityName: nameof(Nota),
            entityId: nota.Id.ToString(),
            summary: $"Nota actualizada: '{nota.Titulo}' (Archivada={nota.Archivada}).",
            cancellationToken: cancellationToken);

        return new NotasOperationResult(NotasOperationStatus.Success, NotaId: nota.Id);
    }

    public async Task<NotasOperationResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var nota = await ApplySandboxIsolation(dbContext.Set<Nota>())
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        if (nota is null)
        {
            return new NotasOperationResult(
                NotasOperationStatus.NotFound,
                ErrorMessage: "La nota solicitada no existe o ya fue eliminada.");
        }

        var titulo = nota.Titulo;
        dbContext.Set<Nota>().Remove(nota);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.LogCriticalActionAsync(
            action: "Notas.Delete",
            entityName: nameof(Nota),
            entityId: id.ToString(),
            summary: $"Borrado lógico de nota: '{titulo}'.",
            cancellationToken: cancellationToken);

        return new NotasOperationResult(NotasOperationStatus.Success, NotaId: id);
    }

    private IQueryable<Nota> ApplySandboxIsolation(IQueryable<Nota> query)
    {
        if (currentUserService.IsDemoSandboxUser)
        {
            var demoUserId = currentUserService.GetActorIdentifier();
            return query.Where(n => n.CreatedBy == demoUserId);
        }

        return query.Where(n => !n.CreatedBy.StartsWith(DemoSandboxConstants.DemoUserIdPrefix));
    }
}
