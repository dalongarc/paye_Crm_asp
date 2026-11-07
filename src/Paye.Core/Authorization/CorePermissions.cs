namespace Paye.Core.Authorization;

public static class CorePermissions
{
    public const string ClaimType = "paye.permission";

    public const string UsuariosVer = "Usuarios.Ver";
    public const string UsuariosGestionar = "Usuarios.Gestionar";
    public const string RolesGestionar = "Roles.Gestionar";
    public const string AuditoriaVer = "Auditoria.Ver";

    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(UsuariosVer, "Ver listado de usuarios", "Administración", [SystemRoles.Propietario, SystemRoles.Encargado]),
        new(UsuariosGestionar, "Crear, editar y activar/desactivar usuarios", "Administración", [SystemRoles.Propietario]),
        new(RolesGestionar, "Configurar permisos de roles", "Administración", [SystemRoles.Propietario]),
        new(AuditoriaVer, "Consultar registro de auditoría", "Administración", [SystemRoles.Propietario])
    ];
}

public sealed record PermissionDefinition(
    string Code,
    string Description,
    string Group,
    IReadOnlyList<string> DefaultRoles);
