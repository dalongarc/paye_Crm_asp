using Paye.Core.Authorization;

namespace Paye.Core.Modules.Documentacion;

public static class DocumentacionPermissions
{
    public const string GroupName = "Documentacion";

    public const string Ver = "Documentacion.Ver";
    public const string Crear = "Documentacion.Crear";
    public const string Editar = "Documentacion.Editar";
    public const string Eliminar = "Documentacion.Eliminar";
    public const string Exportar = "Documentacion.Exportar";

    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(Ver, "Consultar contratos, presupuestos y configuración de documentación", GroupName,
            [SystemRoles.Propietario, SystemRoles.Encargado, SystemRoles.Empleado]),
        new(Crear, "Crear y duplicar contratos y presupuestos", GroupName,
            [SystemRoles.Propietario, SystemRoles.Encargado]),
        new(Editar, "Editar contratos, presupuestos y datos de cabecera/pie", GroupName,
            [SystemRoles.Propietario, SystemRoles.Encargado]),
        new(Eliminar, "Eliminar contratos y presupuestos (borrado lógico)", GroupName,
            [SystemRoles.Propietario]),
        new(Exportar, "Exportar contratos y presupuestos a PDF A4", GroupName,
            [SystemRoles.Propietario, SystemRoles.Encargado, SystemRoles.Empleado])
    ];
}

public sealed class DocumentacionPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public IEnumerable<PermissionDefinition> GetPermissions() => DocumentacionPermissions.All;
}
