using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Web.Api.Database;

namespace Web.Api.Authorization;

/// <summary>
/// A user's permissions, from our database only (SC-03): <c>users.role</c> joined with
/// <c>role_permissions</c>, cached 5 minutes under <c>permissions:{externalId}</c>. A disabled user has none.
/// An unknown user has none and is <b>not</b> cached, so a just-provisioned account works immediately.
/// </summary>
internal sealed class PermissionProvider(ApplicationDbContext db, HybridCache cache)
{
    private static readonly IReadOnlySet<string> None = new HashSet<string>(StringComparer.Ordinal);

    public async Task<IReadOnlySet<string>> GetForExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        string key = PermissionCacheKeys.ForUser(externalId);

        // null = no such user (don't cache); [] = disabled or role without permissions (cache).
        string[]? permissions = await cache.GetOrCreateAsync(
            key,
            (db, externalId),
            static async (state, ct) => await LoadAsync(state.db, state.externalId, ct),
            PermissionCacheKeys.EntryOptions,
            PermissionCacheKeys.Tags,
            cancellationToken);

        if (permissions is null)
        {
            await cache.RemoveAsync(key, cancellationToken);
            return None;
        }

        return new HashSet<string>(permissions, StringComparer.Ordinal);
    }

    private static async Task<string[]?> LoadAsync(ApplicationDbContext db, string externalId, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Where(u => u.ExternalId == externalId)
            .Select(u => new { u.Role, u.IsDisabled })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (user.IsDisabled)
        {
            return [];
        }

        return await db.RolePermissions
            .Where(rp => rp.Role == user.Role)
            .Select(rp => rp.Permission)
            .OrderBy(p => p)
            .ToArrayAsync(cancellationToken);
    }
}
