using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Paye.Core.Auditing;
using Paye.Core.Common;
using Paye.Core.Files;
using Paye.Infrastructure.Data;

namespace Paye.Web.Controllers;

public sealed class UploadPrivateFileInputModel
{
    [Required(ErrorMessage = "Debe seleccionar un archivo.")]
    public IFormFile? File { get; set; }

    [StringLength(300)]
    public string? Description { get; set; }
}

[Authorize]
public sealed class PrivateFilesController(
    IPrivateFileStorage fileStorage,
    PayeDbContext dbContext,
    ICurrentUserService currentUserService,
    IAuditLogger auditLogger) : Controller
{
    [HttpPost("/files/upload")]
    public async Task<IActionResult> Upload(
        [FromForm] UploadPrivateFileInputModel input,
        CancellationToken cancellationToken)
    {
        if (currentUserService.IsDemoSandboxUser)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "La subida de archivos a disco está deshabilitada en el modo demostración aislado." });
        }

        if (!ModelState.IsValid || input.File is null)
        {
            return BadRequest(new { error = "Archivo no válido." });
        }

        await using var stream = input.File.OpenReadStream();
        var saveResult = await fileStorage.SaveValidatedAsync(
            stream,
            input.File.FileName,
            maxBytes: 5 * 1024 * 1024,
            cancellationToken: cancellationToken);

        if (!saveResult.Succeeded || saveResult.File is null)
        {
            return BadRequest(new { error = saveResult.ErrorMessage ?? "El archivo fue rechazado por validación de seguridad." });
        }

        var safeOriginalName = Path.GetFileName(input.File.FileName);
        if (safeOriginalName.Length > 200)
        {
            safeOriginalName = safeOriginalName[..200];
        }

        var record = new PrivateFileRecord
        {
            Id = Guid.NewGuid(),
            StoredFileName = saveResult.File.StoredFileName,
            OriginalDisplayName = safeOriginalName,
            ContentType = saveResult.File.ContentType,
            SizeBytes = saveResult.File.SizeBytes,
            Description = input.Description?.Trim()
        };

        dbContext.PrivateFiles.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.LogCriticalActionAsync(
            action: "Files.Upload",
            entityName: nameof(PrivateFileRecord),
            entityId: record.Id.ToString(),
            summary: $"Subida de archivo privado validado ({record.ContentType}, {record.SizeBytes} bytes).",
            cancellationToken: cancellationToken);

        return Ok(new
        {
            id = record.Id,
            storedFileName = record.StoredFileName,
            contentType = record.ContentType,
            sizeBytes = record.SizeBytes
        });
    }

    [HttpGet("/files/{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        if (currentUserService.IsDemoSandboxUser)
        {
            return NotFound();
        }

        var record = await dbContext.PrivateFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        if (record is null)
        {
            return NotFound();
        }

        var opened = await fileStorage.OpenReadAsync(record.StoredFileName, cancellationToken);
        if (opened is null)
        {
            return NotFound();
        }

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Disposition"] = $"attachment; filename=\"{Uri.EscapeDataString(record.OriginalDisplayName)}\"";

        return File(opened.Value.Stream, opened.Value.ContentType);
    }

    [HttpPost("/files/{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (currentUserService.IsDemoSandboxUser)
        {
            return Forbid();
        }

        var record = await dbContext.PrivateFiles
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        if (record is null)
        {
            return NotFound();
        }

        // Soft-delete metadata via interceptor and remove physical file from private storage
        dbContext.PrivateFiles.Remove(record);
        await dbContext.SaveChangesAsync(cancellationToken);
        await fileStorage.DeleteAsync(record.StoredFileName, cancellationToken);

        await auditLogger.LogCriticalActionAsync(
            action: "Files.Delete",
            entityName: nameof(PrivateFileRecord),
            entityId: record.Id.ToString(),
            summary: "Borrado lógico de registro y eliminación de archivo privado.",
            cancellationToken: cancellationToken);

        return Ok(new { deleted = true });
    }
}
