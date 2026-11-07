using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Paye.Core.Modules.Clientes;

namespace Paye.Web.Modules.Clientes;

[ApiController]
[Route("api/clientes")]
[Authorize]
public sealed class ClientesApiController(IClientesService clientesService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ClientesPermissions.Ver)]
    [ProducesResponseType<ClientesPagedResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] ClienteFilterQuery query,
        CancellationToken cancellationToken)
    {
        var result = await clientesService.GetPagedAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ClientesPermissions.Ver)]
    [ProducesResponseType<ClienteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var cliente = await clientesService.GetByIdAsync(id, cancellationToken);
        if (cliente is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Cliente no encontrado",
                Detail = $"No se encontró ningún cliente con el ID '{id}'."
            });
        }

        return Ok(cliente);
    }

    [HttpPost]
    [Authorize(Policy = ClientesPermissions.Crear)]
    [ProducesResponseType<ClienteDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateClienteRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await clientesService.CreateAsync(request, cancellationToken);
        if (result.Status != ClientesOperationStatus.Success || !result.ClienteId.HasValue)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error al crear cliente",
                Detail = result.ErrorMessage ?? "No se pudo crear el cliente."
            });
        }

        var created = await clientesService.GetByIdAsync(result.ClienteId.Value, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.ClienteId.Value }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ClientesPermissions.Editar)]
    [ProducesResponseType<ClienteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateClienteRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await clientesService.UpdateAsync(id, request, cancellationToken);

        if (result.Status == ClientesOperationStatus.ConcurrencyConflict)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflicto de concurrencia",
                Detail = result.ErrorMessage ?? "El cliente fue modificado por otro usuario mientras lo editabas. Recarga los datos para ver los cambios más recientes."
            };

            if (result.CurrentCliente is not null)
            {
                problem.Extensions["serverVersion"] = result.CurrentCliente.RowVersion;
                problem.Extensions["currentData"] = result.CurrentCliente;
            }

            return StatusCode(StatusCodes.Status409Conflict, problem);
        }

        if (result.Status == ClientesOperationStatus.NotFound)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Cliente no encontrado",
                Detail = result.ErrorMessage ?? $"No se encontró ningún cliente con el ID '{id}'."
            });
        }

        if (result.Status == ClientesOperationStatus.ValidationError)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error de validación",
                Detail = result.ErrorMessage ?? "Datos del cliente no válidos."
            });
        }

        var updated = await clientesService.GetByIdAsync(id, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ClientesPermissions.Eliminar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await clientesService.DeleteAsync(id, cancellationToken);

        if (result.Status == ClientesOperationStatus.NotFound)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Cliente no encontrado",
                Detail = result.ErrorMessage ?? $"No se encontró ningún cliente con el ID '{id}'."
            });
        }

        return NoContent();
    }
}
