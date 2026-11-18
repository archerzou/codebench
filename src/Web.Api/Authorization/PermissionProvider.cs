namespace Web.Api.Authorization;

/// <summary>
/// Loads a user's permissions by Clerk id. Step 1.3.2 replaces this with the role → permission matrix
/// from the database (cached 5 min under <c>permissions:{externalId}</c>, none for disabled users).
/// Until then nobody holds any permission, so every <c>HasPermission</c> endpoint is closed.
/// </summary>
internal sealed class PermissionProvider
{
    private static readonly IReadOnlySet<string> None = new HashSet<string>(StringComparer.Ordinal);

    public Task<IReadOnlySet<string>> GetForExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(None);
    }
}
