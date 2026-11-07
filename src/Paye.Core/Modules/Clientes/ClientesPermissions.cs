using Paye.Core.Authorization;

namespace Paye.Core.Modules.Clientes;

public static class ClientesPermissions
{
    public const string GroupName = "Clientes";

    public const string Ver = "Clientes.Ver";
    public const string Crear = "Clientes.Crear";
    public const string Editar = "Clientes.Editar";
    public const string Eliminar = "Clientes.Eliminar";

    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(Ver, "Consultar listado y ficha de clientes", GroupName,
            [SystemRoles.Propietario, SystemRoles.Encargado, SystemRoles.Empleado]),
        new(Crear, "Crear nuevos clientes", GroupName,
            [SystemRoles.Propietario, SystemRoles.Encargado]),
        new(Editar, "Modificar datos y estado de clientes", GroupName,
            [SystemRoles.Propietario, SystemRoles.Encargado]),
        new(Eliminar, "Eliminar clientes (borrado lógico)", GroupName,
            [SystemRoles.Propietario])
    ];
}

public sealed class ClientesPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public IEnumerable<PermissionDefinition> GetPermissions() => ClientesPermissions.All;
}
