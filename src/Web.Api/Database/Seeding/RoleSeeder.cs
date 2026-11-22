using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Npgsql;
using Web.Api.Authorization;
using Web.Api.Features.Users;

namespace Web.Api.Database.Seeding;

/// <summary>
/// Makes <c>role_permissions</c> equal to <see cref="RolePermissionMatrix"/>: adds missing rows, removes
/// stale ones. Idempotent and safe to run concurrently (inserts use <c>ON CONFLICT DO NOTHING</c>).
/// Runs on every start (<see cref="RoleSeederHostedService"/>) and with <c>--seed</c> (1.5).
/// </summary>
internal sealed class RoleSeeder(ApplicationDbContext db, HybridCache cache, ILogger<RoleSeeder> logger)
{
    public sealed record SeedResult(int Added, int Removed)
    {
        public bool Changed => Added > 0 || Removed > 0;
    }

    public async Task<SeedResult> SeedAsync(CancellationToken cancellationToken)
    {
        (Role Role, string Permission)[] desired = [.. RolePermissionMatrix.Rows];

        List<(Role Role, string Permission)> existing = [.. (await db.RolePermissions.AsNoTracking().ToListAsync(cancellationToken))
            .Select(rp => (rp.Role, rp.Permission))];

        (Role Role, string Permission)[] stale = [.. existing.Except(desired)];
        foreach ((Role role, string permission) in stale)
        {
            await db.RolePermissions
                .Where(rp => rp.Role == role && rp.Permission == permission)
                .ExecuteDeleteAsync(cancellationToken);
        }

        string[] roles = [.. desired.Select(row => row.Role.ToString())];
        string[] permissions = [.. desired.Select(row => row.Permission)];
        int added = await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO role_permissions (role, permission)
            SELECT * FROM unnest({roles}::text[], {permissions}::text[])
            ON CONFLICT DO NOTHING
            """,
            cancellationToken);

        var result = new SeedResult(added, stale.Length);

        if (result.Changed)
        {
            // Every cached permission set may now be wrong.
            await cache.RemoveByTagAsync(PermissionCacheKeys.Tag, cancellationToken);
            logger.LogInformation("Role matrix synced: {Added} added, {Removed} removed", result.Added, result.Removed);
        }

        return result;
    }

    /// <summary>True when the failure is "the table doesn't exist" — i.e. migrations haven't run.</summary>
    internal static bool IsMissingSchema(Exception exception) =>
        exception is PostgresException { SqlState: PostgresErrorCodes.UndefinedTable } ||
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UndefinedTable };
}
