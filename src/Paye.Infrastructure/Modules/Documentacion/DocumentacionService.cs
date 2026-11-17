using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Paye.Core.Auditing;
using Paye.Core.Common;
using Paye.Core.Demo;
using Paye.Core.Modules.Clientes;
using Paye.Core.Modules.Documentacion;
using Paye.Core.Modules.Facturacion;
using Paye.Infrastructure.Data;
using Paye.Infrastructure.Modules.Facturacion;

namespace Paye.Infrastructure.Modules.Documentacion;

public sealed class DocumentacionService(
    PayeDbContext dbContext,
    ICurrentUserService currentUserService,
    IAuditLogger auditLogger) : IDocumentacionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<DocumentacionPagedResult> GetPagedAsync(
        DocumentacionFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var dbQuery = ApplySandboxIsolation(dbContext.Set<DocumentoComercial>().AsNoTracking());

        if (query.Tipo.HasValue)
        {
            dbQuery = dbQuery.Where(d => d.Tipo == query.Tipo.Value);
        }

        if (query.ClienteId.HasValue && query.ClienteId.Value != Guid.Empty)
        {
            dbQuery = dbQuery.Where(d => d.ClienteId == query.ClienteId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";
            dbQuery = dbQuery.Where(d =>
                EF.Functions.ILike(d.NumeroDocumento, term) ||
                EF.Functions.ILike(d.Titulo, term) ||
                EF.Functions.ILike(d.Subtitulo, term) ||
                EF.Functions.ILike(d.ClienteNombre, term) ||
                EF.Functions.ILike(d.ClienteCifNif, term));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        var items = await dbQuery
            .OrderByDescending(d => d.FechaEmision)
            .ThenByDescending(d => d.SequentialNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DocumentoComercialListItemDto(
                d.Id,
                d.Tipo,
                d.NumeroDocumento,
                d.Titulo,
                d.Subtitulo,
                d.ClienteId,
                d.ClienteNombre,
                d.ClienteCifNif,
                d.FechaEmision,
                d.FechaValidez,
                d.BaseImponible,
                d.TotalIva,
                d.TotalGeneral,
                d.CreatedAtUtc,
                d.UpdatedAtUtc,
                d.RowVersion))
            .ToListAsync(cancellationToken);

        return new DocumentacionPagedResult(items, totalCount, page, pageSize);
    }

    public async Task<DocumentoComercialDetailDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var doc = await ApplySandboxIsolation(dbContext.Set<DocumentoComercial>().AsNoTracking())
            .Include(d => d.Lineas.OrderBy(l => l.Orden))
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        return doc is null ? null : MapToDetailDto(doc);
    }

    public async Task<DocumentacionOperationResult<DocumentoComercialDetailDto>> CreateAsync(
        SaveDocumentoComercialRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.IsDefined(request.Tipo))
        {
            return DocumentacionOperationResult.Validation<DocumentoComercialDetailDto>("El tipo de documento no es válido.");
        }

        Cliente? cliente = null;
        if (request.ClienteId.HasValue && request.ClienteId.Value != Guid.Empty)
        {
            cliente = await dbContext.Set<Cliente>()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ClienteId.Value, cancellationToken);

            if (cliente is null)
            {
                return DocumentacionOperationResult.Validation<DocumentoComercialDetailDto>("El cliente seleccionado no existe.");
            }
        }

        var requireLines = request.Tipo == TipoDocumentoComercial.Presupuesto &&
                           request.Lineas is { Count: > 0 };

        var (calc, calcError) = DocumentoTotalsCalculator.ValidateAndCalculate(request.Lineas, requireAtLeastOneLine: requireLines);
        if (calcError is not null || calc is null)
        {
            return DocumentacionOperationResult.Validation<DocumentoComercialDetailDto>(calcError ?? "Error al validar las líneas del documento.");
        }

        var fechaEmision = request.FechaEmision ?? FiscalTimeZone.GetMadridDate(DateTimeOffset.UtcNow);
        var fechaValidez = request.FechaValidez ?? (request.Tipo == TipoDocumentoComercial.Presupuesto ? fechaEmision.AddDays(30) : null);
        var fiscalYear = fechaEmision.Year;

        var bloques = NormalizeBlocks(request.Bloques, request.TextoOrigenDatos, request.NombreArchivoOrigen);
        var bloquesJson = JsonSerializer.Serialize(bloques, JsonOptions);

        var clienteNombre = !string.IsNullOrWhiteSpace(request.ClienteNombreOverride)
            ? request.ClienteNombreOverride.Trim()
            : (cliente is not null
                ? (!string.IsNullOrWhiteSpace(cliente.NombreComercial) ? cliente.NombreComercial : cliente.NombreFiscal)
                : "Cliente sin asignar");

        var clienteCif = !string.IsNullOrWhiteSpace(request.ClienteCifNifOverride)
            ? request.ClienteCifNifOverride.Trim()
            : (cliente?.CifNif ?? string.Empty);

        var clienteDireccion = !string.IsNullOrWhiteSpace(request.ClienteDireccionOverride)
            ? request.ClienteDireccionOverride.Trim()
            : (cliente is not null ? FormatClienteAddress(cliente) : string.Empty);

        var clienteEmail = !string.IsNullOrWhiteSpace(request.ClienteEmailOverride)
            ? request.ClienteEmailOverride.Trim()
            : (cliente?.Email ?? string.Empty);

        var clienteTelefono = !string.IsNullOrWhiteSpace(request.ClienteTelefonoOverride)
            ? request.ClienteTelefonoOverride.Trim()
            : (cliente?.Telefono ?? string.Empty);

        var defaultTitle = request.Tipo == TipoDocumentoComercial.Presupuesto
            ? $"Propuesta comercial — {clienteNombre}"
            : $"Contrato de prestación de servicios — {clienteNombre}";

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var (seqNumber, numeroDocumento, allocError) = await AllocateNextDocumentNumberAsync(
            request.Tipo,
            fiscalYear,
            cancellationToken);

        if (allocError is not null || !seqNumber.HasValue || string.IsNullOrWhiteSpace(numeroDocumento))
        {
            await tx.RollbackAsync(cancellationToken);
            return DocumentacionOperationResult.Validation<DocumentoComercialDetailDto>(
                allocError ?? "No se pudo asignar el número correlativo del documento.");
        }

        var entity = new DocumentoComercial
        {
            Id = Guid.NewGuid(),
            Tipo = request.Tipo,
            FiscalYear = fiscalYear,
            SequentialNumber = seqNumber.Value,
            NumeroDocumento = numeroDocumento,
            Titulo = string.IsNullOrWhiteSpace(request.Titulo) ? defaultTitle : request.Titulo.Trim(),
            Subtitulo = request.Subtitulo?.Trim() ?? string.Empty,
            Categoria = request.Categoria?.Trim() ?? (request.Tipo == TipoDocumentoComercial.Presupuesto ? "Propuesta Comercial" : "Contrato de Servicios"),
            ClienteId = cliente?.Id,
            ClienteNombre = clienteNombre,
            ClienteCifNif = clienteCif,
            ClienteDireccion = clienteDireccion,
            ClienteEmail = clienteEmail,
            ClienteTelefono = clienteTelefono,
            FechaEmision = fechaEmision,
            FechaValidez = fechaValidez,
            ObjetoContrato = request.ObjetoContrato?.Trim(),
            DuracionContrato = request.DuracionContrato?.Trim(),
            PrecioTextoContrato = request.PrecioTextoContrato?.Trim(),
            FormaPagoContrato = request.FormaPagoContrato?.Trim(),
            LugarFirma = request.LugarFirma?.Trim(),
            BloquesJson = bloquesJson,
            SubtotalBruto = calc.SubtotalBruto,
            TotalDescuento = calc.TotalDescuento,
            BaseImponible = calc.BaseImponible,
            TotalIva = calc.TotalIva,
            TotalGeneral = calc.TotalGeneral,
            Lineas = calc.Lineas.Select(l => new DocumentoLinea
            {
                Id = Guid.NewGuid(),
                Orden = l.Orden,
                Descripcion = l.Descripcion,
                Cantidad = l.Cantidad,
                PrecioUnitario = l.PrecioUnitario,
                DescuentoPorcentaje = l.DescuentoPorcentaje,
                IvaPorcentaje = l.IvaPorcentaje,
                ImporteBruto = l.ImporteBruto,
                ImporteDescuento = l.ImporteDescuento,
                BaseImponible = l.BaseImponible,
                CuotaIva = l.CuotaIva,
                TotalLinea = l.TotalLinea
            }).ToList()
        };

        dbContext.Set<DocumentoComercial>().Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        await auditLogger.LogCriticalActionAsync(
            action: $"Documentacion.{entity.Tipo}.Create",
            entityName: nameof(DocumentoComercial),
            entityId: entity.Id.ToString(),
            summary: $"{entity.Tipo} {entity.NumeroDocumento} creado para cliente '{entity.ClienteNombre}' (Total: {entity.TotalGeneral:0.00} €).",
            cancellationToken: cancellationToken);

        return DocumentacionOperationResult.Ok(MapToDetailDto(entity));
    }

    public async Task<DocumentacionOperationResult<DocumentoComercialDetailDto>> UpdateAsync(
        Guid id,
        SaveDocumentoComercialRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await ApplySandboxIsolation(dbContext.Set<DocumentoComercial>())
            .Include(d => d.Lineas)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (entity is null)
        {
            return DocumentacionOperationResult.NotFound<DocumentoComercialDetailDto>("El documento indicado no existe.");
        }

        Cliente? cliente = null;
        if (request.ClienteId.HasValue && request.ClienteId.Value != Guid.Empty)
        {
            cliente = await dbContext.Set<Cliente>()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ClienteId.Value, cancellationToken);

            if (cliente is null)
            {
                return DocumentacionOperationResult.Validation<DocumentoComercialDetailDto>("El cliente seleccionado no existe.");
            }
        }

        var (calc, calcError) = DocumentoTotalsCalculator.ValidateAndCalculate(request.Lineas, requireAtLeastOneLine: false);
        if (calcError is not null || calc is null)
        {
            return DocumentacionOperationResult.Validation<DocumentoComercialDetailDto>(calcError ?? "Error al validar las líneas del documento.");
        }

        if (request.RowVersion > 0)
        {
            dbContext.Entry(entity).Property(x => x.RowVersion).OriginalValue = request.RowVersion;
        }

        var clienteChanged = entity.ClienteId != cliente?.Id;
        entity.ClienteId = cliente?.Id;

        entity.ClienteNombre = !string.IsNullOrWhiteSpace(request.ClienteNombreOverride)
            ? request.ClienteNombreOverride.Trim()
            : (cliente is not null && (clienteChanged || string.IsNullOrWhiteSpace(entity.ClienteNombre))
                ? (!string.IsNullOrWhiteSpace(cliente.NombreComercial) ? cliente.NombreComercial : cliente.NombreFiscal)
                : entity.ClienteNombre);

        entity.ClienteCifNif = request.ClienteCifNifOverride is not null
            ? request.ClienteCifNifOverride.Trim()
            : (cliente is not null && (clienteChanged || string.IsNullOrWhiteSpace(entity.ClienteCifNif)) ? cliente.CifNif : entity.ClienteCifNif);

        entity.ClienteDireccion = request.ClienteDireccionOverride is not null
            ? request.ClienteDireccionOverride.Trim()
            : (cliente is not null && (clienteChanged || string.IsNullOrWhiteSpace(entity.ClienteDireccion)) ? FormatClienteAddress(cliente) : entity.ClienteDireccion);

        entity.ClienteEmail = request.ClienteEmailOverride is not null
            ? request.ClienteEmailOverride.Trim()
            : (cliente is not null && clienteChanged ? cliente.Email : entity.ClienteEmail);

        entity.ClienteTelefono = request.ClienteTelefonoOverride is not null
            ? request.ClienteTelefonoOverride.Trim()
            : (cliente is not null && clienteChanged ? cliente.Telefono : entity.ClienteTelefono);

        if (!string.IsNullOrWhiteSpace(request.Titulo))
        {
            entity.Titulo = request.Titulo.Trim();
        }

        entity.Subtitulo = request.Subtitulo?.Trim() ?? string.Empty;
        entity.Categoria = request.Categoria?.Trim() ?? entity.Categoria;

        if (request.FechaEmision.HasValue)
        {
            entity.FechaEmision = request.FechaEmision.Value;
        }

        entity.FechaValidez = request.FechaValidez;
        entity.ObjetoContrato = request.ObjetoContrato?.Trim();
        entity.DuracionContrato = request.DuracionContrato?.Trim();
        entity.PrecioTextoContrato = request.PrecioTextoContrato?.Trim();
        entity.FormaPagoContrato = request.FormaPagoContrato?.Trim();
        entity.LugarFirma = request.LugarFirma?.Trim();

        if (request.Bloques is not null || request.TextoOrigenDatos is not null)
        {
            var currentBlocks = request.Bloques;
            if (currentBlocks is null)
            {
                var existingDetail = MapToDetailDto(entity);
                currentBlocks = existingDetail.Bloques;
            }

            var bloques = NormalizeBlocks(currentBlocks, request.TextoOrigenDatos, request.NombreArchivoOrigen);
            entity.BloquesJson = JsonSerializer.Serialize(bloques, JsonOptions);
        }

        entity.SubtotalBruto = calc.SubtotalBruto;
        entity.TotalDescuento = calc.TotalDescuento;
        entity.BaseImponible = calc.BaseImponible;
        entity.TotalIva = calc.TotalIva;
        entity.TotalGeneral = calc.TotalGeneral;

        dbContext.Set<DocumentoLinea>().RemoveRange(entity.Lineas);
        entity.Lineas = calc.Lineas.Select(l => new DocumentoLinea
        {
            Id = Guid.NewGuid(),
            DocumentoId = entity.Id,
            Orden = l.Orden,
            Descripcion = l.Descripcion,
            Cantidad = l.Cantidad,
            PrecioUnitario = l.PrecioUnitario,
            DescuentoPorcentaje = l.DescuentoPorcentaje,
            IvaPorcentaje = l.IvaPorcentaje,
            ImporteBruto = l.ImporteBruto,
            ImporteDescuento = l.ImporteDescuento,
            BaseImponible = l.BaseImponible,
            CuotaIva = l.CuotaIva,
            TotalLinea = l.TotalLinea
        }).ToList();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return DocumentacionOperationResult.Conflict<DocumentoComercialDetailDto>();
        }

        await auditLogger.LogCriticalActionAsync(
            action: $"Documentacion.{entity.Tipo}.Update",
            entityName: nameof(DocumentoComercial),
            entityId: entity.Id.ToString(),
            summary: $"{entity.Tipo} {entity.NumeroDocumento} actualizado (Total: {entity.TotalGeneral:0.00} €).",
            cancellationToken: cancellationToken);

        return DocumentacionOperationResult.Ok(MapToDetailDto(entity));
    }

    public async Task<DocumentacionOperationResult<DocumentoComercialDetailDto>> DuplicateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var original = await ApplySandboxIsolation(dbContext.Set<DocumentoComercial>().AsNoTracking())
            .Include(d => d.Lineas.OrderBy(l => l.Orden))
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (original is null)
        {
            return DocumentacionOperationResult.NotFound<DocumentoComercialDetailDto>("El documento a duplicar no existe.");
        }

        var today = FiscalTimeZone.GetMadridDate(DateTimeOffset.UtcNow);
        var fiscalYear = today.Year;

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var (seqNumber, numeroDocumento, allocError) = await AllocateNextDocumentNumberAsync(
            original.Tipo,
            fiscalYear,
            cancellationToken);

        if (allocError is not null || !seqNumber.HasValue || string.IsNullOrWhiteSpace(numeroDocumento))
        {
            await tx.RollbackAsync(cancellationToken);
            return DocumentacionOperationResult.Validation<DocumentoComercialDetailDto>(
                allocError ?? "No se pudo asignar el número correlativo para el duplicado.");
        }

        var clone = new DocumentoComercial
        {
            Id = Guid.NewGuid(),
            Tipo = original.Tipo,
            FiscalYear = fiscalYear,
            SequentialNumber = seqNumber.Value,
            NumeroDocumento = numeroDocumento,
            Titulo = $"{original.Titulo} (Copia)",
            Subtitulo = original.Subtitulo,
            Categoria = original.Categoria,
            ClienteId = original.ClienteId,
            ClienteNombre = original.ClienteNombre,
            ClienteCifNif = original.ClienteCifNif,
            ClienteDireccion = original.ClienteDireccion,
            ClienteEmail = original.ClienteEmail,
            ClienteTelefono = original.ClienteTelefono,
            FechaEmision = today,
            FechaValidez = original.Tipo == TipoDocumentoComercial.Presupuesto ? today.AddDays(30) : null,
            ObjetoContrato = original.ObjetoContrato,
            DuracionContrato = original.DuracionContrato,
            PrecioTextoContrato = original.PrecioTextoContrato,
            FormaPagoContrato = original.FormaPagoContrato,
            LugarFirma = original.LugarFirma,
            BloquesJson = original.BloquesJson,
            SubtotalBruto = original.SubtotalBruto,
            TotalDescuento = original.TotalDescuento,
            BaseImponible = original.BaseImponible,
            TotalIva = original.TotalIva,
            TotalGeneral = original.TotalGeneral,
            Lineas = original.Lineas.Select(l => new DocumentoLinea
            {
                Id = Guid.NewGuid(),
                Orden = l.Orden,
                Descripcion = l.Descripcion,
                Cantidad = l.Cantidad,
                PrecioUnitario = l.PrecioUnitario,
                DescuentoPorcentaje = l.DescuentoPorcentaje,
                IvaPorcentaje = l.IvaPorcentaje,
                ImporteBruto = l.ImporteBruto,
                ImporteDescuento = l.ImporteDescuento,
                BaseImponible = l.BaseImponible,
                CuotaIva = l.CuotaIva,
                TotalLinea = l.TotalLinea
            }).ToList()
        };

        dbContext.Set<DocumentoComercial>().Add(clone);
        await dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        await auditLogger.LogCriticalActionAsync(
            action: $"Documentacion.{clone.Tipo}.Duplicate",
            entityName: nameof(DocumentoComercial),
            entityId: clone.Id.ToString(),
            summary: $"{clone.Tipo} {clone.NumeroDocumento} duplicado a partir de {original.NumeroDocumento}.",
            cancellationToken: cancellationToken);

        return DocumentacionOperationResult.Ok(MapToDetailDto(clone));
    }

    public async Task<DocumentacionOperationResult<bool>> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await ApplySandboxIsolation(dbContext.Set<DocumentoComercial>())
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (entity is null)
        {
            return DocumentacionOperationResult.NotFound<bool>("El documento no existe.");
        }

        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTimeOffset.UtcNow;
        entity.DeletedBy = currentUserService.GetActorIdentifier();

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.LogCriticalActionAsync(
            action: $"Documentacion.{entity.Tipo}.Delete",
            entityName: nameof(DocumentoComercial),
            entityId: entity.Id.ToString(),
            summary: $"{entity.Tipo} {entity.NumeroDocumento} eliminado (borrado lógico).",
            cancellationToken: cancellationToken);

        return DocumentacionOperationResult.Ok(true);
    }

    public async Task<DocumentacionCompanySettingsDto> GetCompanySettingsAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await EnsureCompanySettingsEntityAsync(cancellationToken);
        return MapSettingsToDto(settings);
    }

    public async Task<DocumentacionOperationResult<DocumentacionCompanySettingsDto>> UpdateCompanySettingsAsync(
        UpdateDocumentacionCompanySettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!DocumentoHtmlSanitizer.IsValidLogoOrImageUrl(request.LogoUrl))
        {
            return DocumentacionOperationResult.Validation<DocumentacionCompanySettingsDto>(
                "La URL del logotipo no es válida. Solo se permiten rutas locales (/images/...) o Data URI de imagen (PNG/JPEG/WEBP).");
        }

        var settings = await EnsureCompanySettingsEntityAsync(cancellationToken);

        if (request.RowVersion > 0)
        {
            dbContext.Entry(settings).Property(x => x.RowVersion).OriginalValue = request.RowVersion;
        }

        settings.NombreEmpresa = DocumentoHtmlSanitizer.SanitizeText(request.NombreEmpresa);
        settings.CifNif = DocumentoHtmlSanitizer.SanitizeText(request.CifNif);
        settings.Direccion = DocumentoHtmlSanitizer.SanitizeText(request.Direccion);
        settings.Telefono = DocumentoHtmlSanitizer.SanitizeText(request.Telefono);
        settings.Email = DocumentoHtmlSanitizer.SanitizeText(request.Email);
        settings.Web = DocumentoHtmlSanitizer.SanitizeText(request.Web);
        settings.Iban = DocumentoHtmlSanitizer.SanitizeText(request.Iban);
        settings.LogoUrl = request.LogoUrl?.Trim() ?? string.Empty;
        var cleanLegalTitle = string.IsNullOrWhiteSpace(request.TextoLegalTitulo)
            ? "Propiedad Intelectual y Confidencialidad"
            : DocumentoHtmlSanitizer.SanitizeText(request.TextoLegalTitulo);
        var cleanModel = NormalizeAiModelName(request.ModeloIa);
        settings.TextoLegalTitulo = PackLegalTitleWithAiModel(cleanLegalTitle, cleanModel);
        settings.TextoLegalContenido = DocumentoHtmlSanitizer.SanitizeText(request.TextoLegalContenido);

        if (!string.IsNullOrWhiteSpace(request.GeminiApiKey))
        {
            DocumentoGeminiCompletionProvider.SetRuntimeApiKey(request.GeminiApiKey);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return DocumentacionOperationResult.Conflict<DocumentacionCompanySettingsDto>();
        }

        await auditLogger.LogCriticalActionAsync(
            action: "Documentacion.Settings.Update",
            entityName: nameof(DocumentacionCompanySettings),
            entityId: settings.Id.ToString(),
            summary: $"Ajustes de empresa para Documentación actualizados ('{settings.NombreEmpresa}').",
            cancellationToken: cancellationToken);

        return DocumentacionOperationResult.Ok(MapSettingsToDto(settings));
    }

    /// <summary>
    /// Asignación atómica y segura ante concurrencia en PostgreSQL mediante UPSERT + RETURNING
    /// con bloqueo de fila sobre ("Tipo", "FiscalYear"). Reinicia en 001 automáticamente cada año.
    /// </summary>
    private async Task<(int? SequentialNumber, string? NumeroDocumento, string? ErrorMessage)> AllocateNextDocumentNumberAsync(
        TipoDocumentoComercial tipo,
        int fiscalYear,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var actor = currentUserService.GetActorIdentifier();
        var codigo = DocumentoSerie.GetDefaultCode(tipo);
        var tipoInt = (int)tipo;
        var newId = Guid.NewGuid();

        var allocated = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                INSERT INTO documentacion_series ("Id", "Tipo", "Codigo", "FiscalYear", "LastNumber", "PaddingDigits", "CreatedAtUtc", "CreatedBy")
                VALUES ({0}, {1}, {2}, {3}, 1, 3, {4}, {5})
                ON CONFLICT ("Tipo", "FiscalYear")
                DO UPDATE SET "LastNumber" = documentacion_series."LastNumber" + 1,
                              "UpdatedAtUtc" = {4},
                              "UpdatedBy" = {5}
                RETURNING "LastNumber" AS "Value"
                """,
                newId,
                tipoInt,
                codigo,
                fiscalYear,
                now,
                actor)
            .ToListAsync(cancellationToken);

        if (allocated.Count == 0)
        {
            return (null, null, "No se pudo reservar el número correlativo en la base de datos.");
        }

        var seq = allocated[0];
        var formatted = DocumentoSerie.FormatNumber(tipo, fiscalYear, seq, paddingDigits: 3);
        return (seq, formatted, null);
    }

    private async Task<DocumentacionCompanySettings> EnsureCompanySettingsEntityAsync(CancellationToken cancellationToken)
    {
        var existing = await dbContext.Set<DocumentacionCompanySettings>()
            .OrderBy(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        // Si existen ajustes fiscales previos en Facturación, los tomamos como base editable inicial
        var billing = await dbContext.Set<BillingSettings>()
            .AsNoTracking()
            .OrderBy(b => b.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var created = new DocumentacionCompanySettings
        {
            Id = Guid.NewGuid(),
            NombreEmpresa = !string.IsNullOrWhiteSpace(billing?.TaxName) ? billing.TaxName : "Payé Digital",
            CifNif = billing?.TaxId ?? string.Empty,
            Direccion = billing?.FiscalAddress ?? string.Empty,
            Telefono = string.Empty,
            Email = string.Empty,
            Web = string.Empty,
            Iban = string.Empty,
            LogoUrl = "/images/documentacion/logo-paye-dark.png",
            TextoLegalTitulo = "Propiedad Intelectual y Confidencialidad",
            TextoLegalContenido =
                "El contenido de este documento es propiedad exclusiva de la empresa emisora. Queda prohibida la reproducción parcial o total, así como su distribución a terceros sin autorización expresa. La información técnica, económica y metodológica aquí descrita se presenta bajo un marco de confidencialidad estrictamente comercial."
        };

        dbContext.Set<DocumentacionCompanySettings>().Add(created);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(created).State = EntityState.Detached;
            return await dbContext.Set<DocumentacionCompanySettings>().OrderBy(s => s.CreatedAtUtc).FirstAsync(cancellationToken);
        }

        return created;
    }

    private IQueryable<DocumentoComercial> ApplySandboxIsolation(IQueryable<DocumentoComercial> query)
    {
        if (currentUserService.IsDemoSandboxUser)
        {
            var demoUserId = currentUserService.GetActorIdentifier();
            return query.Where(d => d.CreatedBy == demoUserId);
        }

        return query.Where(d => !d.CreatedBy.StartsWith(DemoSandboxConstants.DemoUserIdPrefix));
    }

    private static string FormatClienteAddress(Cliente cliente)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(cliente.Direccion)) parts.Add(cliente.Direccion.Trim());

        var locality = string.Join(" ", new[] { cliente.CodigoPostal?.Trim(), cliente.Ciudad?.Trim() }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(locality)) parts.Add(locality);

        if (!string.IsNullOrWhiteSpace(cliente.Provincia)) parts.Add($"({cliente.Provincia.Trim()})");
        return string.Join(", ", parts);
    }

    private const string MetaOrigenDatosBlockId = "__meta_origen_datos__";
    private const string MetaOrigenDatosBlockType = "meta-origen-datos";

    private static List<DocumentoBloqueDto> NormalizeBlocks(
        IReadOnlyList<DocumentoBloqueDto>? blocks,
        string? textoOrigenDatos = null,
        string? nombreArchivoOrigen = null)
    {
        var result = new List<DocumentoBloqueDto>();

        if (blocks is not null && blocks.Count > 0)
        {
            result.AddRange(blocks
                .Where(b => !string.Equals(b.Id, MetaOrigenDatosBlockId, StringComparison.Ordinal) &&
                            !string.Equals(b.TipoBloque, MetaOrigenDatosBlockType, StringComparison.Ordinal))
                .OrderBy(b => b.Orden)
                .Select((b, idx) => new DocumentoBloqueDto(
                    Id: string.IsNullOrWhiteSpace(b.Id) ? Guid.NewGuid().ToString("N") : DocumentoHtmlSanitizer.SanitizeText(b.Id),
                    TemplateBlockId: string.IsNullOrWhiteSpace(b.TemplateBlockId) ? null : DocumentoHtmlSanitizer.SanitizeText(b.TemplateBlockId),
                    TipoBloque: string.IsNullOrWhiteSpace(b.TipoBloque) ? "pill-section" : DocumentoHtmlSanitizer.SanitizeText(b.TipoBloque),
                    NumeroPastilla: string.IsNullOrWhiteSpace(b.NumeroPastilla) ? null : DocumentoHtmlSanitizer.SanitizeText(b.NumeroPastilla),
                    Titulo: DocumentoHtmlSanitizer.SanitizeText(b.Titulo),
                    ContenidoHtml: DocumentoHtmlSanitizer.SanitizeHtml(b.ContenidoHtml),
                    Visible: b.Visible,
                    Orden: idx + 1,
                    EsPersonalizado: b.EsPersonalizado)));
        }

        if (!string.IsNullOrWhiteSpace(textoOrigenDatos))
        {
            var trimmedSource = textoOrigenDatos.Length > 60_000
                ? textoOrigenDatos[..60_000]
                : textoOrigenDatos;
            var safeFileName = string.IsNullOrWhiteSpace(nombreArchivoOrigen)
                ? string.Empty
                : DocumentoHtmlSanitizer.SanitizeText(nombreArchivoOrigen);

            result.Add(new DocumentoBloqueDto(
                Id: MetaOrigenDatosBlockId,
                TemplateBlockId: null,
                TipoBloque: MetaOrigenDatosBlockType,
                NumeroPastilla: null,
                Titulo: safeFileName,
                ContenidoHtml: trimmedSource,
                Visible: false,
                Orden: 9999,
                EsPersonalizado: false));
        }

        return result;
    }

    private static DocumentoComercialDetailDto MapToDetailDto(DocumentoComercial doc)
    {
        List<DocumentoBloqueDto> rawBloques;
        try
        {
            rawBloques = string.IsNullOrWhiteSpace(doc.BloquesJson)
                ? []
                : (JsonSerializer.Deserialize<List<DocumentoBloqueDto>>(doc.BloquesJson, JsonOptions) ?? []);
        }
        catch
        {
            rawBloques = [];
        }

        var metaOrigen = rawBloques.FirstOrDefault(b =>
            string.Equals(b.Id, MetaOrigenDatosBlockId, StringComparison.Ordinal) ||
            string.Equals(b.TipoBloque, MetaOrigenDatosBlockType, StringComparison.Ordinal));

        var bloques = rawBloques
            .Where(b => !string.Equals(b.Id, MetaOrigenDatosBlockId, StringComparison.Ordinal) &&
                        !string.Equals(b.TipoBloque, MetaOrigenDatosBlockType, StringComparison.Ordinal))
            .OrderBy(b => b.Orden)
            .ToList();

        var orderedLines = doc.Lineas.OrderBy(l => l.Orden).ToList();
        var lineDtos = orderedLines.Select(l => new DocumentoLineaDto(
            l.Id,
            l.Orden,
            l.Descripcion,
            l.Cantidad,
            l.PrecioUnitario,
            l.DescuentoPorcentaje,
            l.IvaPorcentaje,
            l.ImporteBruto,
            l.ImporteDescuento,
            l.BaseImponible,
            l.CuotaIva,
            l.TotalLinea)).ToList();

        var desgloseIva = orderedLines
            .GroupBy(l => l.IvaPorcentaje)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var baseGrupo = DocumentoTotalsCalculator.RoundCurrency(g.Sum(x => x.BaseImponible));
                var cuotaGrupo = DocumentoTotalsCalculator.RoundCurrency(g.Sum(x => x.CuotaIva));
                var totalGrupo = DocumentoTotalsCalculator.RoundCurrency(baseGrupo + cuotaGrupo);

                return new DesgloseIvaItemDto(
                    IvaPorcentaje: g.Key,
                    Etiqueta: g.Key == 0m ? "Exento / 0%" : $"IVA {g.Key:0.##}%",
                    BaseImponible: baseGrupo,
                    CuotaIva: cuotaGrupo,
                    TotalConIva: totalGrupo);
            })
            .ToList();

        return new DocumentoComercialDetailDto(
            Id: doc.Id,
            Tipo: doc.Tipo,
            FiscalYear: doc.FiscalYear,
            SequentialNumber: doc.SequentialNumber,
            NumeroDocumento: doc.NumeroDocumento,
            Titulo: doc.Titulo,
            Subtitulo: doc.Subtitulo,
            Categoria: doc.Categoria,
            ClienteId: doc.ClienteId,
            ClienteNombre: doc.ClienteNombre,
            ClienteCifNif: doc.ClienteCifNif,
            ClienteDireccion: doc.ClienteDireccion,
            ClienteEmail: doc.ClienteEmail,
            ClienteTelefono: doc.ClienteTelefono,
            FechaEmision: doc.FechaEmision,
            FechaValidez: doc.FechaValidez,
            ObjetoContrato: doc.ObjetoContrato,
            DuracionContrato: doc.DuracionContrato,
            PrecioTextoContrato: doc.PrecioTextoContrato,
            FormaPagoContrato: doc.FormaPagoContrato,
            LugarFirma: doc.LugarFirma,
            Bloques: bloques,
            Lineas: lineDtos,
            DesgloseIva: desgloseIva,
            SubtotalBruto: doc.SubtotalBruto,
            TotalDescuento: doc.TotalDescuento,
            BaseImponible: doc.BaseImponible,
            TotalIva: doc.TotalIva,
            TotalGeneral: doc.TotalGeneral,
            CreatedAtUtc: doc.CreatedAtUtc,
            UpdatedAtUtc: doc.UpdatedAtUtc,
            RowVersion: doc.RowVersion,
            TextoOrigenDatos: metaOrigen?.ContenidoHtml,
            NombreArchivoOrigen: string.IsNullOrWhiteSpace(metaOrigen?.Titulo) ? null : metaOrigen.Titulo);
    }

    private const string AiModelDelimiter = "||modelo:";

    private static string NormalizeAiModelName(string? model)
    {
        var trimmed = model?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "gemini-3.8-flash";
        }

        // Permitir únicamente identificadores de modelo seguros (letras, números, guiones y puntos)
        var sanitized = new string(trimmed.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '.' || c == '_').ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "gemini-3.8-flash" : sanitized;
    }

    private static string PackLegalTitleWithAiModel(string cleanTitle, string cleanModel)
    {
        var baseTitle = UnpackLegalTitleAndAiModel(cleanTitle).CleanTitle;
        var combined = $"{baseTitle}{AiModelDelimiter}{cleanModel}";
        return combined.Length <= 150 ? combined : baseTitle;
    }

    private static (string CleanTitle, string AiModel) UnpackLegalTitleAndAiModel(string? storedTitle)
    {
        if (string.IsNullOrWhiteSpace(storedTitle))
        {
            return ("Propiedad Intelectual y Confidencialidad", "gemini-3.8-flash");
        }

        var idx = storedTitle.IndexOf(AiModelDelimiter, StringComparison.Ordinal);
        if (idx < 0)
        {
            return (storedTitle.Trim(), "gemini-3.8-flash");
        }

        var title = storedTitle[..idx].Trim();
        var model = NormalizeAiModelName(storedTitle[(idx + AiModelDelimiter.Length)..]);
        return (string.IsNullOrWhiteSpace(title) ? "Propiedad Intelectual y Confidencialidad" : title, model);
    }

    private static DocumentacionCompanySettingsDto MapSettingsToDto(DocumentacionCompanySettings s)
    {
        var (cleanTitle, aiModel) = UnpackLegalTitleAndAiModel(s.TextoLegalTitulo);
        return new DocumentacionCompanySettingsDto(
            s.Id,
            s.NombreEmpresa,
            s.CifNif,
            s.Direccion,
            s.Telefono,
            s.Email,
            s.Web,
            s.Iban,
            s.LogoUrl,
            cleanTitle,
            s.TextoLegalContenido,
            s.RowVersion,
            aiModel);
    }
}
