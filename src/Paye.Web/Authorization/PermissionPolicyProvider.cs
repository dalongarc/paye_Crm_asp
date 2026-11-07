using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Paye.Core.Authorization;

namespace Paye.Web.Authorization;

public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;
    private readonly HashSet<string> _knownPermissions;

    public PermissionPolicyProvider(
        IOptions<AuthorizationOptions> options,
        IEnumerable<IPermissionDefinitionProvider> permissionProviders)
    {
        _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
        _knownPermissions = permissionProviders
            .SelectMany(p => p.GetPermissions())
            .Select(p => p.Code)
            .ToHashSet(StringComparer.Ordinal);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (_knownPermissions.Contains(policyName) || policyName.Contains('.', StringComparison.Ordinal))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallbackPolicyProvider.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _fallbackPolicyProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _fallbackPolicyProvider.GetFallbackPolicyAsync();
}
