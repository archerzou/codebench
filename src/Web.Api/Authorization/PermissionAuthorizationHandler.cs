using Microsoft.AspNetCore.Authorization;
using Web.Api.Authentication;

namespace Web.Api.Authorization;

/// <summary>
/// Grants a permission requirement only if the caller is authenticated and our database grants the
/// permission (SC-03). Fails closed: anonymous, unknown or permission-less callers are denied.
/// </summary>
internal sealed class PermissionAuthorizationHandler(IServiceScopeFactory serviceScopeFactory)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        string? externalId = context.User.GetExternalId();

        if (externalId is null)
        {
            return;
        }

        using IServiceScope scope = serviceScopeFactory.CreateScope();

        PermissionProvider permissionProvider = scope.ServiceProvider.GetRequiredService<PermissionProvider>();

        IReadOnlySet<string> permissions = await permissionProvider.GetForExternalIdAsync(externalId);

        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
