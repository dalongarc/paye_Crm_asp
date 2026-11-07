using System.ComponentModel.DataAnnotations;
using Paye.Core.Authorization;

namespace Paye.Web.ViewModels.Admin;

public sealed class UsersIndexViewModel
{
    public IReadOnlyList<UserSummaryItem> Users { get; init; } = [];
    public IReadOnlyList<RolePermissionsItem> Roles { get; init; } = [];
    public IReadOnlyList<PermissionDefinition> AvailablePermissions { get; init; } = [];
}

public sealed record UserSummaryItem(
    string Id,
    string Email,
    string FullName,
    bool IsActive,
    bool TwoFactorEnabled,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<string> Roles);

public sealed record RolePermissionsItem(
    string RoleName,
    bool IsEditable,
    IReadOnlySet<string> GrantedPermissions);

public sealed class CreateUserViewModel : IValidatableObject
{
    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña inicial es obligatoria.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "La contraseña debe tener un mínimo de 12 caracteres.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "El rol es obligatorio.")]
    public string Role { get; set; } = SystemRoles.Empleado;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!SystemRoles.All.Contains(Role, StringComparer.Ordinal))
        {
            yield return new ValidationResult(
                "El rol seleccionado no es válido.",
                [nameof(Role)]);
        }
    }
}

public sealed class UpdateUserRoleViewModel : IValidatableObject
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!SystemRoles.All.Contains(Role, StringComparer.Ordinal))
        {
            yield return new ValidationResult(
                "El rol seleccionado no es válido.",
                [nameof(Role)]);
        }
    }
}

public sealed class UpdateRolePermissionsViewModel : IValidatableObject
{
    [Required]
    public string RoleName { get; set; } = string.Empty;

    public string[] SelectedPermissions { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!SystemRoles.All.Contains(RoleName, StringComparer.Ordinal))
        {
            yield return new ValidationResult(
                "El rol indicado no existe.",
                [nameof(RoleName)]);
        }

        if (string.Equals(RoleName, SystemRoles.Propietario, StringComparison.Ordinal))
        {
            yield return new ValidationResult(
                "Los permisos del rol Propietario no se pueden restringir.",
                [nameof(RoleName)]);
        }
    }
}
