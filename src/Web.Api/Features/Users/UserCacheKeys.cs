namespace Web.Api.Features.Users;

/// <summary>HybridCache keys owned by the Users feature (QY-05).</summary>
public static class UserCacheKeys
{
    /// <summary>Clerk <c>sub</c> → local user id. Invalidated by the webhook (update/delete) and role changes.</summary>
    public static string ByExternalId(string externalId) => $"users:ext:{externalId}";

    public static readonly TimeSpan ByExternalIdExpiration = TimeSpan.FromMinutes(30);
}
