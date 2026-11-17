using Paye.Core.Navigation;

namespace Paye.Core.Modules.Documentacion;

public sealed class DocumentacionNavigationProvider : IModuleNavigationProvider
{
    public IEnumerable<MenuItemDefinition> GetMenuItems()
    {
        yield return new MenuItemDefinition(
            Title: "Documentación",
            Url: "/documentacion",
            IconCssClass: "FileText",
            RequiredPermission: DocumentacionPermissions.Ver,
            Order: 25);
    }
}
