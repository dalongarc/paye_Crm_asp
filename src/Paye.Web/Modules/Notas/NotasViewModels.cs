using System.ComponentModel.DataAnnotations;
using Paye.Core.Modules.Notas;

namespace Paye.Web.Modules.Notas;

public sealed class NotasIndexViewModel
{
    public required NotasPagedResult PagedResult { get; init; }
    public string? SearchTerm { get; init; }
    public string? EstadoFiltro { get; init; }
    public bool CanCreate { get; init; }
    public bool CanEdit { get; init; }
    public bool CanDelete { get; init; }
}

public sealed class CreateNotaInputModel
{
    [Required(ErrorMessage = "El título es obligatorio.")]
    [StringLength(150, ErrorMessage = "El título no puede superar los 150 caracteres.")]
    public string Titulo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El contenido es obligatorio.")]
    [StringLength(4000, ErrorMessage = "El contenido no puede superar los 4000 caracteres.")]
    public string Contenido { get; set; } = string.Empty;

    public bool Archivada { get; set; }
}

public sealed class EditNotaInputModel
{
    [Required]
    public Guid Id { get; set; }

    [Required(ErrorMessage = "El título es obligatorio.")]
    [StringLength(150, ErrorMessage = "El título no puede superar los 150 caracteres.")]
    public string Titulo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El contenido es obligatorio.")]
    [StringLength(4000, ErrorMessage = "El contenido no puede superar los 4000 caracteres.")]
    public string Contenido { get; set; } = string.Empty;

    public bool Archivada { get; set; }

    [Required]
    public uint RowVersion { get; set; }
}

public sealed class DeleteNotaConfirmViewModel
{
    public required NotaDto Nota { get; init; }
}
