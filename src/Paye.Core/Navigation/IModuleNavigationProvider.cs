namespace Paye.Core.Navigation;

public sealed record MenuItemDefinition(
    string Title,
    string Url,
    string IconCssClass,
    string? RequiredPermission = null,
    int Order = 100);

public interface IModuleNavigationProvider
{
    IEnumerable<MenuItemDefinition> GetMenuItems();
}
