namespace Paye.Core.Authorization;

public static class SystemRoles
{
    public const string Propietario = "Propietario";
    public const string Encargado = "Encargado";
    public const string Empleado = "Empleado";

    public static readonly IReadOnlyList<string> All =
    [
        Propietario,
        Encargado,
        Empleado
    ];
}
