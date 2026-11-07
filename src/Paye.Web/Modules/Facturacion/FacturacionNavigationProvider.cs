using Paye.Core.Modules.Facturacion;
using Paye.Core.Navigation;

namespace Paye.Web.Modules.Facturacion;

public sealed class FacturacionNavigationProvider : IModuleNavigationProvider
{
    public IEnumerable<MenuItemDefinition> GetMenuItems()
    {
        yield return new MenuItemDefinition(
            Title: "Facturación",
            Url: "/facturacion",
            IconCssClass: "icon-facturacion",
            RequiredPermission: FacturacionPermissions.Ver,
            Order: 10);
    }
}
