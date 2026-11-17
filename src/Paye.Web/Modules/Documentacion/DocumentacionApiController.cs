using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Paye.Core.Modules.Documentacion;

namespace Paye.Web.Modules.Documentacion;

[ApiController]
[Route("api/documentacion")]
[Authorize]
public sealed class DocumentacionApiController(
    IDocumentacionService documentacionService,
    IDocumentoPdfExportService pdfExportService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = DocumentacionPermissions.Ver)]
    [ProducesResponseType<DocumentacionPagedResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] DocumentacionFilterQuery query,
        CancellationToken cancellationToken)
    {
        var result = await documentacionService.GetPagedAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = DocumentacionPermissions.Ver)]
    [ProducesResponseType<DocumentoComercialDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var doc = await documentacionService.GetByIdAsync(id, cancellationToken);
        if (doc is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Documento no encontrado",
                Detail = $"No se encontró ningún documento con el ID '{id}'."
            });
        }

        return Ok(doc);
    }

    [HttpPost]
    [Authorize(Policy = DocumentacionPermissions.Crear)]
    [ProducesResponseType<DocumentoComercialDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] SaveDocumentoComercialRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await documentacionService.CreateAsync(request, cancellationToken);
        if (!result.Succeeded || result.Data is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error al crear documento",
                Detail = result.ErrorMessage ?? "No se pudo crear el documento."
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data.Id }, result.Data);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = DocumentacionPermissions.Editar)]
    [ProducesResponseType<DocumentoComercialDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] SaveDocumentoComercialRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await documentacionService.UpdateAsync(id, request, cancellationToken);

        if (result.IsNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Documento no encontrado",
                Detail = result.ErrorMessage ?? "El documento no existe."
            });
        }

        if (result.IsConflict)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflicto de concurrencia",
                Detail = result.ErrorMessage ?? "El documento fue modificado por otro usuario."
            });
        }

        if (!result.Succeeded || result.Data is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error de validación",
                Detail = result.ErrorMessage ?? "Datos del documento no válidos."
            });
        }

        return Ok(result.Data);
    }

    [HttpPost("{id:guid}/duplicar")]
    [Authorize(Policy = DocumentacionPermissions.Crear)]
    [ProducesResponseType<DocumentoComercialDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Duplicate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await documentacionService.DuplicateAsync(id, cancellationToken);

        if (result.IsNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Documento no encontrado",
                Detail = result.ErrorMessage ?? "El documento a duplicar no existe."
            });
        }

        if (!result.Succeeded || result.Data is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error al duplicar documento",
                Detail = result.ErrorMessage ?? "No se pudo duplicar el documento."
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data.Id }, result.Data);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = DocumentacionPermissions.Eliminar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await documentacionService.DeleteAsync(id, cancellationToken);
        if (result.IsNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Documento no encontrado",
                Detail = result.ErrorMessage ?? "El documento no existe."
            });
        }

        return NoContent();
    }

    [HttpGet("ajustes")]
    [Authorize(Policy = DocumentacionPermissions.Ver)]
    [ProducesResponseType<DocumentacionCompanySettingsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCompanySettings(CancellationToken cancellationToken)
    {
        var settings = await documentacionService.GetCompanySettingsAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpPut("ajustes")]
    [Authorize(Policy = DocumentacionPermissions.Editar)]
    [ProducesResponseType<DocumentacionCompanySettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCompanySettings(
        [FromBody] UpdateDocumentacionCompanySettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await documentacionService.UpdateCompanySettingsAsync(request, cancellationToken);
        if (result.IsConflict)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflicto de concurrencia",
                Detail = result.ErrorMessage
            });
        }

        if (!result.Succeeded || result.Data is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error al guardar ajustes",
                Detail = result.ErrorMessage ?? "No se pudieron actualizar los ajustes."
            });
        }

        return Ok(result.Data);
    }

    [HttpPost("calcular-lineas")]
    [Authorize(Policy = DocumentacionPermissions.Ver)]
    [ProducesResponseType<CalculatedDocumentoTotals>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public IActionResult CalculateLines([FromBody] IReadOnlyList<DocumentoLineaInputDto> lineas)
    {
        var (result, error) = DocumentoTotalsCalculator.ValidateAndCalculate(lineas, requireAtLeastOneLine: false);
        if (error is not null || result is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error de cálculo",
                Detail = error ?? "Las líneas enviadas no son válidas."
            });
        }

        return Ok(result);
    }

    [HttpGet("{id:guid}/pdf")]
    [Authorize(Policy = DocumentacionPermissions.Exportar)]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportPdf(
        Guid id,
        [FromQuery] DocumentoA4LayoutOptionsDto? layoutOptions,
        CancellationToken cancellationToken)
    {
        var exportResult = await pdfExportService.ExportPdfByIdAsync(id, layoutOptions, cancellationToken);
        if (exportResult.IsNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Documento no encontrado",
                Detail = exportResult.ErrorMessage ?? "El documento solicitado no existe."
            });
        }

        if (!exportResult.Succeeded || exportResult.Data is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error al exportar PDF",
                Detail = exportResult.ErrorMessage ?? "No se pudo generar el PDF del documento."
            });
        }

        return File(exportResult.Data.PdfBytes, "application/pdf", exportResult.Data.FileName);
    }
}
