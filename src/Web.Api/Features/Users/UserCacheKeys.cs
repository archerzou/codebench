using Microsoft.Extensions.Caching.Hybrid;

namespace Web.Api.Features.Users;

/// <summary>HybridCache keys and entry options owned by the Users feature (QY-05).</summary>
public static class UserCacheKeys
{
    public static readonly TimeSpan Expiration = TimeSpan.FromMinutes(30);

    public static readonly HybridCacheEntryOptions EntryOptions = new()
    {
        Expiration = Expiration,
        LocalCacheExpiration = Expiration
    };

    /// <summary>Clerk <c>sub</c> → local user id. Invalidated by the webhook (update/delete) and role changes.</summary>
    public static string ByExternalId(string externalId) => $"users:ext:{externalId}";
}
