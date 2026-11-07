namespace Paye.Core.Authorization;

public interface IPermissionDefinitionProvider
{
    IEnumerable<PermissionDefinition> GetPermissions();
}

public sealed class CorePermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public IEnumerable<PermissionDefinition> GetPermissions() => CorePermissions.All;
}
