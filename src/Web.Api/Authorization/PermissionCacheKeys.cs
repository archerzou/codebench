using Microsoft.Extensions.Caching.Hybrid;

namespace Web.Api.Authorization;

/// <summary>HybridCache keys and entry options for permissions (QY-05).</summary>
public static class PermissionCacheKeys
{
    /// <summary>On every permission entry: <c>RoleSeeder</c> clears them all when the matrix changes.</summary>
    public const string Tag = "permissions";

    public static readonly TimeSpan Expiration = TimeSpan.FromMinutes(5);

    public static readonly HybridCacheEntryOptions EntryOptions = new()
    {
        Expiration = Expiration,
        LocalCacheExpiration = Expiration
    };

    public static readonly IReadOnlyCollection<string> Tags = [Tag];

    /// <summary>Per user. Removed by <c>ChangeUserRole</c> / <c>DisableUser</c> (3A) and by tag on matrix changes.</summary>
    public static string ForUser(string externalId) => $"permissions:{externalId}";
}
