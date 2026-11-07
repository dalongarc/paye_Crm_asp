using Paye.Core.Authorization;

namespace Paye.Core.Modules.Facturacion;

public static class FacturacionPermissions
{
    public const string GroupName = "Facturacion";

    public const string Ver = "Facturacion.Ver";
    public const string Crear = "Facturacion.Crear";
    public const string Emitir = "Facturacion.Emitir";
    public const string Rectificar = "Facturacion.Rectificar";
    public const string Descargar = "Facturacion.Descargar";
    public const string Configurar = "Facturacion.Configurar";

    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(Ver, "Consultar presupuestos, borradores, facturas emitidas y rectificativas", GroupName,
            [SystemRoles.Propietario, SystemRoles.Encargado, SystemRoles.Empleado]),
        new(Crear, "Crear y editar presupuestos y borradores de factura; eliminar borradores", GroupName,
            [SystemRoles.Propietario, SystemRoles.Encargado, SystemRoles.Empleado]),
        new(Emitir, "Emitir definitivamente facturas (consume numeración fiscal inmutable)", GroupName,
            [SystemRoles.Propietario, SystemRoles.Encargado]),
        new(Rectificar, "Emitir facturas rectificativas sobre facturas emitidas", GroupName,
            [SystemRoles.Propietario, SystemRoles.Encargado]),
        new(Descargar, "Descargar PDF de facturas y presupuestos (auditado)", GroupName,
            [SystemRoles.Propietario, SystemRoles.Encargado, SystemRoles.Empleado]),
        new(Configurar, "Configurar datos fiscales del emisor, plantilla y series de facturación", GroupName,
            [SystemRoles.Propietario])
    ];
}

public sealed class FacturacionPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public IEnumerable<PermissionDefinition> GetPermissions() => FacturacionPermissions.All;
}
