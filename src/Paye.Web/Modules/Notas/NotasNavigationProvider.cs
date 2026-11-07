using Paye.Core.Modules.Notas;
using Paye.Core.Navigation;

namespace Paye.Web.Modules.Notas;

public sealed class NotasNavigationProvider : IModuleNavigationProvider
{
    public IEnumerable<MenuItemDefinition> GetMenuItems()
    {
        yield return new MenuItemDefinition(
            Title: "Notas",
            Url: "/notas",
            IconCssClass: "icon-notas",
            RequiredPermission: NotasPermissions.Ver,
            Order: 20);
    }
}
