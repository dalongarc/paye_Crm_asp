using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Paye.Core.Authorization;
using Paye.Core.Navigation;

namespace Paye.Web.ViewComponents;

public sealed record SidebarMenuItemViewModel(
    string Title,
    string Url,
    string IconCssClass,
    bool IsActive);

public sealed class SidebarNavigationViewComponent(
    IEnumerable<IModuleNavigationProvider> moduleNavigationProviders,
    IAuthorizationService authorizationService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (HttpContext.User.Identity?.IsAuthenticated != true)
        {
            return View(Array.Empty<SidebarMenuItemViewModel>());
        }

        var currentPath = HttpContext.Request.Path.Value ?? "/";

        var candidateItems = new List<MenuItemDefinition>
        {
            new("Inicio", "/", "icon-home", RequiredPermission: null, Order: 1)
        };

        foreach (var provider in moduleNavigationProviders)
        {
            candidateItems.AddRange(provider.GetMenuItems());
        }

        candidateItems.Add(new(
            "Usuarios y roles",
            "/admin/users",
            "icon-users",
            RequiredPermission: CorePermissions.UsuariosVer,
            Order: 900));

        candidateItems.Add(new(
            "Auditoría",
            "/admin/audit",
            "icon-audit",
            RequiredPermission: CorePermissions.AuditoriaVer,
            Order: 910));

        var authorizedItems = new List<SidebarMenuItemViewModel>();

        foreach (var item in candidateItems.OrderBy(i => i.Order).ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(item.RequiredPermission))
            {
                var authResult = await authorizationService.AuthorizeAsync(HttpContext.User, item.RequiredPermission);
                if (!authResult.Succeeded)
                {
                    continue;
                }
            }

            var isActive = string.Equals(item.Url, "/", StringComparison.OrdinalIgnoreCase)
                ? string.Equals(currentPath, "/", StringComparison.OrdinalIgnoreCase)
                : currentPath.StartsWith(item.Url, StringComparison.OrdinalIgnoreCase);

            authorizedItems.Add(new SidebarMenuItemViewModel(
                item.Title,
                item.Url,
                item.IconCssClass,
                isActive));
        }

        return View(authorizedItems);
    }
}
