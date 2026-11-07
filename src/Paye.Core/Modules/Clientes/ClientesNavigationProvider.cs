using Paye.Core.Navigation;

namespace Paye.Core.Modules.Clientes;

public sealed class ClientesNavigationProvider : IModuleNavigationProvider
{
    public IEnumerable<MenuItemDefinition> GetMenuItems()
    {
        yield return new MenuItemDefinition(
            Title: "Clientes",
            Url: "/clientes",
            IconCssClass: "Users",
            RequiredPermission: ClientesPermissions.Ver,
            Order: 20);
    }
}
