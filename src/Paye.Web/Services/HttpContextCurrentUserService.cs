using System.Security.Claims;
using Paye.Core.Common;
using Paye.Core.Demo;

namespace Paye.Web.Services;

public sealed class HttpContextCurrentUserService(
    IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public string? UserId =>
        httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserEmail =>
        httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email)
        ?? httpContextAccessor.HttpContext?.User?.Identity?.Name;

    public string? IpAddress =>
        httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public bool IsDemoSandboxUser =>
        DemoSandboxConstants.IsDemoUserId(UserId) ||
        string.Equals(
            httpContextAccessor.HttpContext?.User?.FindFirstValue(DemoSandboxConstants.ClaimType),
            DemoSandboxConstants.ClaimValue,
            StringComparison.Ordinal);
}
