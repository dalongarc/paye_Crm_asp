using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Paye.Core.Modules.Notas;

namespace Paye.Web.Modules.Notas;

[Authorize(Policy = NotasPermissions.Ver)]
public sealed class NotasController(
    INotasService notasService,
    IAuthorizationService authorizationService) : Controller
{
    [HttpGet("/notas")]
    public async Task<IActionResult> Index(
        [FromQuery] string? q = null,
        [FromQuery] string? estado = null,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        bool? archivadaFilter = estado?.ToLowerInvariant() switch
        {
            "activas" => false,
            "archivadas" => true,
            _ => null
        };

        var pagedResult = await notasService.GetPagedAsync(
            new NotaFilterQuery(
                SearchTerm: q,
                Archivada: archivadaFilter,
                Page: Math.Max(1, page),
                PageSize: 10),
            cancellationToken);

        var canCreate = (await authorizationService.AuthorizeAsync(User, NotasPermissions.Crear)).Succeeded;
        var canEdit = (await authorizationService.AuthorizeAsync(User, NotasPermissions.Editar)).Succeeded;
        var canDelete = (await authorizationService.AuthorizeAsync(User, NotasPermissions.Borrar)).Succeeded;

        var vm = new NotasIndexViewModel
        {
            PagedResult = pagedResult,
            SearchTerm = q,
            EstadoFiltro = estado,
            CanCreate = canCreate,
            CanEdit = canEdit,
            CanDelete = canDelete
        };

        return View(vm);
    }

    [HttpGet("/notas/crear")]
    [Authorize(Policy = NotasPermissions.Crear)]
    public IActionResult Create()
    {
        return View(new CreateNotaInputModel());
    }

    [HttpPost("/notas/crear")]
    [Authorize(Policy = NotasPermissions.Crear)]
    public async Task<IActionResult> Create(
        CreateNotaInputModel input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var result = await notasService.CreateAsync(
            new CreateNotaCommand(input.Titulo, input.Contenido, input.Archivada),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No se pudo crear la nota.");
            return View(input);
        }

        TempData["SuccessMessage"] = "Nota creada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("/notas/{id:guid}/editar")]
    [Authorize(Policy = NotasPermissions.Editar)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var nota = await notasService.GetByIdAsync(id, cancellationToken);
        if (nota is null)
        {
            TempData["ErrorMessage"] = "La nota solicitada no existe.";
            return RedirectToAction(nameof(Index));
        }

        var vm = new EditNotaInputModel
        {
            Id = nota.Id,
            Titulo = nota.Titulo,
            Contenido = nota.Contenido,
            Archivada = nota.Archivada,
            RowVersion = nota.RowVersion
        };

        return View(vm);
    }

    [HttpPost("/notas/{id:guid}/editar")]
    [Authorize(Policy = NotasPermissions.Editar)]
    public async Task<IActionResult> Edit(
        Guid id,
        EditNotaInputModel input,
        CancellationToken cancellationToken)
    {
        if (id != input.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var result = await notasService.UpdateAsync(
            new UpdateNotaCommand(
                input.Id,
                input.Titulo,
                input.Contenido,
                input.Archivada,
                input.RowVersion),
            cancellationToken);

        if (result.Status == NotasOperationStatus.NotFound)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "La nota no existe.";
            return RedirectToAction(nameof(Index));
        }

        if (result.Status == NotasOperationStatus.ConcurrencyConflict)
        {
            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage ?? "Conflicto de concurrencia: otro usuario modificó esta nota.");

            var latest = await notasService.GetByIdAsync(id, cancellationToken);
            if (latest is not null)
            {
                ModelState.Remove(nameof(EditNotaInputModel.RowVersion));
                input.RowVersion = latest.RowVersion;
            }

            return View(input);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No se pudo actualizar la nota.");
            return View(input);
        }

        TempData["SuccessMessage"] = "Nota actualizada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("/notas/{id:guid}/borrar")]
    [Authorize(Policy = NotasPermissions.Borrar)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var nota = await notasService.GetByIdAsync(id, cancellationToken);
        if (nota is null)
        {
            TempData["ErrorMessage"] = "La nota solicitada no existe.";
            return RedirectToAction(nameof(Index));
        }

        return View(new DeleteNotaConfirmViewModel { Nota = nota });
    }

    [HttpPost("/notas/{id:guid}/borrar")]
    [Authorize(Policy = NotasPermissions.Borrar)]
    public async Task<IActionResult> DeleteConfirmed(Guid id, CancellationToken cancellationToken)
    {
        var result = await notasService.DeleteAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "No se pudo eliminar la nota.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = "Nota eliminada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("/notas/api")]
    public async Task<IActionResult> GetNotasJson(
        [FromQuery] string? q = null,
        [FromQuery] string? estado = null,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        bool? archivadaFilter = estado?.ToLowerInvariant() switch
        {
            "activas" => false,
            "archivadas" => true,
            _ => null
        };

        var pagedResult = await notasService.GetPagedAsync(
            new NotaFilterQuery(
                SearchTerm: q,
                Archivada: archivadaFilter,
                Page: Math.Max(1, page),
                PageSize: 50),
            cancellationToken);

        return Ok(new
        {
            items = pagedResult.Items,
            totalCount = pagedResult.TotalCount,
            page = pagedResult.Page,
            pageSize = pagedResult.PageSize,
            totalPages = pagedResult.TotalPages
        });
    }

    [HttpPost("/notas/api")]
    [Authorize(Policy = NotasPermissions.Crear)]
    public async Task<IActionResult> CreateNotaJson(
        [FromBody] CreateNotaInputModel input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await notasService.CreateAsync(
            new CreateNotaCommand(input.Titulo, input.Contenido, input.Archivada),
            cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.ErrorMessage ?? "No se pudo crear la nota." });
        }

        return Ok(new { success = true, id = result.NotaId });
    }

    [HttpPut("/notas/api/{id:guid}")]
    [Authorize(Policy = NotasPermissions.Editar)]
    public async Task<IActionResult> UpdateNotaJson(
        Guid id,
        [FromBody] EditNotaInputModel input,
        CancellationToken cancellationToken)
    {
        if (id != input.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await notasService.UpdateAsync(
            new UpdateNotaCommand(
                input.Id,
                input.Titulo,
                input.Contenido,
                input.Archivada,
                input.RowVersion),
            cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.ErrorMessage ?? "No se pudo actualizar la nota." });
        }

        return Ok(new { success = true });
    }

    [HttpDelete("/notas/api/{id:guid}")]
    [Authorize(Policy = NotasPermissions.Borrar)]
    public async Task<IActionResult> DeleteNotaJson(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await notasService.DeleteAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.ErrorMessage ?? "No se pudo eliminar la nota." });
        }

        return Ok(new { success = true });
    }
}
