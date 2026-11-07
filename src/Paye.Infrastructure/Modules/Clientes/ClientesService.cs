using Microsoft.EntityFrameworkCore;
using Paye.Core.Auditing;
using Paye.Core.Modules.Clientes;
using Paye.Infrastructure.Data;

namespace Paye.Infrastructure.Modules.Clientes;

public sealed class ClientesService(
    PayeDbContext dbContext,
    IAuditLogger auditLogger) : IClientesService
{
    public async Task<ClientesPagedResult> GetPagedAsync(
        ClienteFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        IQueryable<Cliente> clientesQuery = dbContext.Set<Cliente>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            clientesQuery = clientesQuery.Where(c =>
                c.NombreFiscal.Contains(term) ||
                c.NombreComercial.Contains(term) ||
                c.CifNif.Contains(term) ||
                c.Email.Contains(term));
        }

        if (query.Activo.HasValue)
        {
            var activo = query.Activo.Value;
            clientesQuery = clientesQuery.Where(c => c.Activo == activo);
        }

        clientesQuery = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("nombrefiscal", false) => clientesQuery.OrderBy(c => c.NombreFiscal),
            ("nombrefiscal", true) => clientesQuery.OrderByDescending(c => c.NombreFiscal),
            ("cifnif", false) => clientesQuery.OrderBy(c => c.CifNif),
            ("cifnif", true) => clientesQuery.OrderByDescending(c => c.CifNif),
            ("ciudad", false) => clientesQuery.OrderBy(c => c.Ciudad),
            ("ciudad", true) => clientesQuery.OrderByDescending(c => c.Ciudad),
            ("createdat", false) => clientesQuery.OrderBy(c => c.CreatedAtUtc),
            ("createdat", true) => clientesQuery.OrderByDescending(c => c.CreatedAtUtc),
            _ => clientesQuery.OrderByDescending(c => c.CreatedAtUtc)
        };

        var totalCount = await clientesQuery.CountAsync(cancellationToken);

        var items = await clientesQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => MapToDto(c))
            .ToListAsync(cancellationToken);

        return new ClientesPagedResult(items, totalCount, page, pageSize);
    }

    public async Task<ClienteDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<Cliente>().AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => MapToDto(c))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ClientesOperationResult> CreateAsync(
        CreateClienteRequest request,
        CancellationToken cancellationToken = default)
    {
        var nombreFiscal = request.NombreFiscal.Trim();
        var cifNif = request.CifNif.Trim().ToUpperInvariant();

        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            NombreFiscal = nombreFiscal,
            NombreComercial = request.NombreComercial?.Trim() ?? string.Empty,
            CifNif = cifNif,
            Email = request.Email?.Trim() ?? string.Empty,
            Telefono = request.Telefono?.Trim() ?? string.Empty,
            Direccion = request.Direccion?.Trim() ?? string.Empty,
            CodigoPostal = request.CodigoPostal?.Trim() ?? string.Empty,
            Ciudad = request.Ciudad?.Trim() ?? string.Empty,
            Provincia = request.Provincia?.Trim() ?? string.Empty,
            Activo = request.Activo,
            Notas = request.Notas?.Trim()
        };

        dbContext.Set<Cliente>().Add(cliente);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.LogCriticalActionAsync(
            action: "Clientes.Create",
            entityName: nameof(Cliente),
            entityId: cliente.Id.ToString(),
            summary: $"Cliente creado: '{cliente.NombreFiscal}' ({cliente.CifNif}).",
            cancellationToken: cancellationToken);

        return new ClientesOperationResult(ClientesOperationStatus.Success, ClienteId: cliente.Id);
    }

    public async Task<ClientesOperationResult> UpdateAsync(
        Guid id,
        UpdateClienteRequest request,
        CancellationToken cancellationToken = default)
    {
        var cliente = await dbContext.Set<Cliente>()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (cliente is null)
        {
            return new ClientesOperationResult(
                ClientesOperationStatus.NotFound,
                ErrorMessage: "El cliente solicitado no existe o fue eliminado.");
        }

        // Concurrencia optimista mediante columna xmin (RowVersion) de PostgreSQL
        dbContext.Entry(cliente).Property(c => c.RowVersion).OriginalValue = request.RowVersion;

        cliente.NombreFiscal = request.NombreFiscal.Trim();
        cliente.NombreComercial = request.NombreComercial?.Trim() ?? string.Empty;
        cliente.CifNif = request.CifNif.Trim().ToUpperInvariant();
        cliente.Email = request.Email?.Trim() ?? string.Empty;
        cliente.Telefono = request.Telefono?.Trim() ?? string.Empty;
        cliente.Direccion = request.Direccion?.Trim() ?? string.Empty;
        cliente.CodigoPostal = request.CodigoPostal?.Trim() ?? string.Empty;
        cliente.Ciudad = request.Ciudad?.Trim() ?? string.Empty;
        cliente.Provincia = request.Provincia?.Trim() ?? string.Empty;
        cliente.Activo = request.Activo;
        cliente.Notas = request.Notas?.Trim();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            var currentDto = await GetByIdAsync(id, cancellationToken);
            return new ClientesOperationResult(
                ClientesOperationStatus.ConcurrencyConflict,
                ClienteId: cliente.Id,
                ErrorMessage: "El cliente fue modificado por otro usuario mientras lo editaba. Recargue la página para ver los cambios recientes.",
                CurrentCliente: currentDto);
        }

        await auditLogger.LogCriticalActionAsync(
            action: "Clientes.Update",
            entityName: nameof(Cliente),
            entityId: cliente.Id.ToString(),
            summary: $"Cliente actualizado: '{cliente.NombreFiscal}' ({cliente.CifNif}, Activo={cliente.Activo}).",
            cancellationToken: cancellationToken);

        return new ClientesOperationResult(ClientesOperationStatus.Success, ClienteId: cliente.Id);
    }

    public async Task<ClientesOperationResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var cliente = await dbContext.Set<Cliente>()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (cliente is null)
        {
            return new ClientesOperationResult(
                ClientesOperationStatus.NotFound,
                ErrorMessage: "El cliente solicitado no existe o ya fue eliminado.");
        }

        var nombre = cliente.NombreFiscal;
        var cif = cliente.CifNif;

        // El interceptor AuditableEntitySaveChangesInterceptor convierte el borrado físico en lógico (soft delete)
        dbContext.Set<Cliente>().Remove(cliente);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.LogCriticalActionAsync(
            action: "Clientes.Delete",
            entityName: nameof(Cliente),
            entityId: id.ToString(),
            summary: $"Cliente eliminado (borrado lógico): '{nombre}' ({cif}).",
            cancellationToken: cancellationToken);

        return new ClientesOperationResult(ClientesOperationStatus.Success, ClienteId: id);
    }

    private static ClienteDto MapToDto(Cliente c) => new(
        c.Id,
        c.NombreFiscal,
        c.NombreComercial,
        c.CifNif,
        c.Email,
        c.Telefono,
        c.Direccion,
        c.CodigoPostal,
        c.Ciudad,
        c.Provincia,
        c.Activo,
        c.Notas,
        c.RowVersion,
        c.CreatedAtUtc,
        c.CreatedBy,
        c.UpdatedAtUtc,
        c.UpdatedBy);
}
