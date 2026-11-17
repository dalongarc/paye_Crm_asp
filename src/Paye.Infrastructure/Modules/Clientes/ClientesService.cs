using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Paye.Core.Auditing;
using Paye.Core.Modules.Clientes;
using Paye.Infrastructure.Data;

namespace Paye.Infrastructure.Modules.Clientes;

public sealed class ClientesService(
    PayeDbContext dbContext,
    IAuditLogger auditLogger) : IClientesService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

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
                c.Email.Contains(term) ||
                c.Telefono.Contains(term));
        }

        if (query.Activo.HasValue)
        {
            var activo = query.Activo.Value;
            clientesQuery = clientesQuery.Where(c => c.Activo == activo);
        }

        if (query.EsBorrador.HasValue)
        {
            var esBorrador = query.EsBorrador.Value;
            clientesQuery = clientesQuery.Where(c => c.EsBorrador == esBorrador);
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

        var entities = await clientesQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = entities.Select(MapToDto).ToList();

        return new ClientesPagedResult(items, totalCount, page, pageSize);
    }

    public async Task<ClienteDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Set<Cliente>().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return entity is null ? null : MapToDto(entity);
    }

    public async Task<ClientesOperationResult> CreateAsync(
        CreateClienteRequest request,
        CancellationToken cancellationToken = default)
    {
        var nombreFiscal = request.NombreFiscal.Trim();
        var cifNif = request.CifNif?.Trim().ToUpperInvariant() ?? string.Empty;
        var esBorrador = string.IsNullOrWhiteSpace(cifNif) || (request.EsBorrador ?? false);
        var departamentos = NormalizeDepartamentos(request.Departamentos);

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
            EsBorrador = esBorrador,
            DepartamentosJson = JsonSerializer.Serialize(departamentos, JsonOptions),
            Notas = request.Notas?.Trim()
        };

        dbContext.Set<Cliente>().Add(cliente);
        await dbContext.SaveChangesAsync(cancellationToken);

        var cifLabel = string.IsNullOrWhiteSpace(cliente.CifNif) ? "BORRADOR SIN CIF/NIF" : cliente.CifNif;
        await auditLogger.LogCriticalActionAsync(
            action: "Clientes.Create",
            entityName: nameof(Cliente),
            entityId: cliente.Id.ToString(),
            summary: $"Cliente creado: '{cliente.NombreFiscal}' ({cifLabel}, Dptos={departamentos.Count}).",
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

        var cifNif = request.CifNif?.Trim().ToUpperInvariant() ?? string.Empty;
        var esBorrador = string.IsNullOrWhiteSpace(cifNif) || (request.EsBorrador ?? false);

        cliente.NombreFiscal = request.NombreFiscal.Trim();
        cliente.NombreComercial = request.NombreComercial?.Trim() ?? string.Empty;
        cliente.CifNif = cifNif;
        cliente.Email = request.Email?.Trim() ?? string.Empty;
        cliente.Telefono = request.Telefono?.Trim() ?? string.Empty;
        cliente.Direccion = request.Direccion?.Trim() ?? string.Empty;
        cliente.CodigoPostal = request.CodigoPostal?.Trim() ?? string.Empty;
        cliente.Ciudad = request.Ciudad?.Trim() ?? string.Empty;
        cliente.Provincia = request.Provincia?.Trim() ?? string.Empty;
        cliente.Activo = request.Activo;
        cliente.EsBorrador = esBorrador;
        cliente.Notas = request.Notas?.Trim();

        if (request.Departamentos is not null)
        {
            var departamentos = NormalizeDepartamentos(request.Departamentos);
            cliente.DepartamentosJson = JsonSerializer.Serialize(departamentos, JsonOptions);
        }

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

        var cifLabel = string.IsNullOrWhiteSpace(cliente.CifNif) ? "BORRADOR SIN CIF/NIF" : cliente.CifNif;
        await auditLogger.LogCriticalActionAsync(
            action: "Clientes.Update",
            entityName: nameof(Cliente),
            entityId: cliente.Id.ToString(),
            summary: $"Cliente actualizado: '{cliente.NombreFiscal}' ({cifLabel}, Activo={cliente.Activo}, Borrador={cliente.EsBorrador}).",
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
        var cif = string.IsNullOrWhiteSpace(cliente.CifNif) ? "SIN CIF/NIF" : cliente.CifNif;

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

    private static List<ClienteDepartamentoDto> NormalizeDepartamentos(IReadOnlyList<ClienteDepartamentoDto>? items)
    {
        if (items is null || items.Count == 0)
        {
            return [];
        }

        var result = new List<ClienteDepartamentoDto>();
        foreach (var d in items)
        {
            var nombre = d.Nombre?.Trim() ?? string.Empty;
            var telefono = d.Telefono?.Trim() ?? string.Empty;
            var persona = d.PersonaContacto?.Trim() ?? string.Empty;
            var email = d.Email?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(nombre) &&
                string.IsNullOrWhiteSpace(telefono) &&
                string.IsNullOrWhiteSpace(persona) &&
                string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            result.Add(new ClienteDepartamentoDto(
                Id: string.IsNullOrWhiteSpace(d.Id) ? Guid.NewGuid().ToString("N") : d.Id.Trim(),
                Nombre: string.IsNullOrWhiteSpace(nombre) ? "General" : (nombre.Length > 100 ? nombre[..100] : nombre),
                Telefono: string.IsNullOrWhiteSpace(telefono) ? null : (telefono.Length > 30 ? telefono[..30] : telefono),
                PersonaContacto: string.IsNullOrWhiteSpace(persona) ? null : (persona.Length > 100 ? persona[..100] : persona),
                Email: string.IsNullOrWhiteSpace(email) ? null : (email.Length > 100 ? email[..100] : email)));
        }

        return result;
    }

    private static ClienteDto MapToDto(Cliente c)
    {
        List<ClienteDepartamentoDto> departamentos;
        try
        {
            departamentos = string.IsNullOrWhiteSpace(c.DepartamentosJson)
                ? []
                : (JsonSerializer.Deserialize<List<ClienteDepartamentoDto>>(c.DepartamentosJson, JsonOptions) ?? []);
        }
        catch
        {
            departamentos = [];
        }

        var esBorrador = c.EsBorrador || string.IsNullOrWhiteSpace(c.CifNif);

        return new ClienteDto(
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
            c.UpdatedBy,
            esBorrador,
            departamentos);
    }
}
