namespace Web.Api.Authentication;

/// <summary>The caller of the current request. Handlers use this, never <c>HttpContext</c> (VS-04).</summary>
public interface IUserContext
{
    bool IsAuthenticated { get; }

    /// <summary>Clerk user id (<c>sub</c>). Throws <see cref="UserContextUnavailableException"/> when anonymous.</summary>
    string ExternalId { get; }

    /// <summary>
    /// Local user id, resolved once per request by <see cref="UserResolutionMiddleware"/> (cached under
    /// <c>users:ext:{sub}</c>). Throws when anonymous or not provisioned — use <see cref="TryGetUserId"/>
    /// where "no local account yet" is an expected case.
    /// </summary>
    Guid UserId { get; }

    bool TryGetUserId(out Guid userId);
}
