using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Paye.Core.Modules.Documentacion;

namespace Paye.Web.Modules.Documentacion;

[ApiController]
[Route("api/documentacion/ai")]
[Authorize]
public sealed class DocumentacionAiApiController(
    IDocumentoAiGenerationService aiService) : ControllerBase
{
    [HttpPost("scan-secrets")]
    [Authorize(Policy = DocumentacionPermissions.Editar)]
    [ProducesResponseType<DocumentoSecretScanResult>(StatusCodes.Status200OK)]
    public IActionResult ScanSecrets([FromBody] EscanearSecretosAiRequest request)
    {
        var result = aiService.ScanSecrets(request.TextoOrigen);
        return Ok(result);
    }

    [HttpPost("extract-file")]
    [Authorize(Policy = DocumentacionPermissions.Editar)]
    [ProducesResponseType<DocumentoArchivoExtraidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExtractFile(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Archivo vacío",
                Detail = "Debes seleccionar un archivo válido (.md, .txt o .pdf)."
            });
        }

        await using var stream = file.OpenReadStream();
        var result = await aiService.ExtractTextFromUploadedFileAsync(
            stream,
            file.FileName,
            file.Length,
            cancellationToken);

        if (!result.Succeeded || result.Data is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error al leer el archivo",
                Detail = result.ErrorMessage ?? "No se pudo extraer el texto del archivo."
            });
        }

        return Ok(result.Data);
    }

    [HttpPost("generar-borrador")]
    [Authorize(Policy = DocumentacionPermissions.Editar)]
    [ProducesResponseType<DocumentoAiBorradorResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> GenerarBorrador(
        [FromBody] GenerarBorradorAiRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await aiService.GenerarBorradorAsync(request, cancellationToken);
        if (!result.Succeeded || result.Data is null)
        {
            return BuildErrorResult(result);
        }

        return Ok(result.Data);
    }

    [HttpPost("regenerar-bloque")]
    [Authorize(Policy = DocumentacionPermissions.Editar)]
    [ProducesResponseType<DocumentoAiBloqueGeneradoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> RegenerarBloque(
        [FromBody] RegenerarBloqueAiRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await aiService.RegenerarBloqueAsync(request, cancellationToken);
        if (!result.Succeeded || result.Data is null)
        {
            return BuildErrorResult(result);
        }

        return Ok(result.Data);
    }

    private IActionResult BuildErrorResult<T>(DocumentoAiOperationResult<T> result)
    {
        if (result.IsRateLimited || result.IsQuotaExceeded)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Límite de peticiones o cuota excedida",
                Detail = result.ErrorMessage ?? "Se ha superado el límite de peticiones de IA. Espera unos segundos e inténtalo de nuevo."
            });
        }

        if (result.IsTimeout)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new ProblemDetails
            {
                Status = StatusCodes.Status504GatewayTimeout,
                Title = "Tiempo de espera agotado",
                Detail = result.ErrorMessage ?? "El servicio de IA tardó demasiado en responder."
            });
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = result.SecretsDetected ? "Secretos detectados en el texto" : "No se pudo completar la generación con IA",
            Detail = result.ErrorMessage ?? "No se pudo procesar la solicitud de generación."
        };

        if (result.SecretsDetected)
        {
            problem.Extensions["secretsDetected"] = true;
            problem.Extensions["secretFindings"] = result.SecretFindings;
            problem.Extensions["redactedSourceText"] = result.RedactedSourceText;
        }

        return BadRequest(problem);
    }
}
