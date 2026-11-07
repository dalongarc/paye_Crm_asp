using Microsoft.AspNetCore.Authorization;

namespace Paye.Web.Authorization;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
