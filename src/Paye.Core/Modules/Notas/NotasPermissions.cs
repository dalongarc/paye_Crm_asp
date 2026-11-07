using Paye.Core.Authorization;

namespace Paye.Core.Modules.Notas;

public static class NotasPermissions
{
    public const string GroupName = "Notas";

    public const string Ver = "Notas.Ver";
    public const string Crear = "Notas.Crear";
    public const string Editar = "Notas.Editar";
    public const string Borrar = "Notas.Borrar";

    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(Ver, "Consultar listado y detalle de notas", GroupName,
            [SystemRoles.Propietario, SystemRoles.Encargado, SystemRoles.Empleado]),
        new(Crear, "Crear nuevas notas", GroupName,
            [SystemRoles.Propietario, SystemRoles.Encargado, SystemRoles.Empleado]),
        new(Editar, "Editar y archivar notas existentes", GroupName,
            [SystemRoles.Propietario, SystemRoles.Encargado]),
        new(Borrar, "Eliminar notas (borrado lógico)", GroupName,
            [SystemRoles.Propietario])
    ];
}

public sealed class NotasPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public IEnumerable<PermissionDefinition> GetPermissions() => NotasPermissions.All;
}
