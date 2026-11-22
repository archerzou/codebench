using Web.Api.Common;

namespace Web.Api.Features.Users;

/// <summary>
/// One row of the role → permission matrix (composite key). Written by <c>RoleSeeder</c> (1.3) on every
/// start; read by <c>PermissionProvider</c>. Clerk claims are never consulted (SC-03).
/// </summary>
public sealed class RolePermission : Entity
{
    private RolePermission()
    {
    }

    public Role Role { get; private set; }

    /// <summary>Permission name, <c>"{area}:{action}"</c> (NM-07), e.g. <c>problems:publish</c>.</summary>
    public string Permission { get; private set; } = string.Empty;

    public static RolePermission Create(Role role, string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        return new RolePermission { Role = role, Permission = permission };
    }
}
