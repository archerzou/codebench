namespace Web.Api.Authentication;

internal sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public string ExternalId =>
        httpContextAccessor.HttpContext?.User.GetExternalId() ?? throw new UserContextUnavailableException();

    public Guid UserId => TryGetUserId(out Guid userId) ? userId : throw new UserContextUnavailableException();

    public bool TryGetUserId(out Guid userId)
    {
        if (httpContextAccessor.HttpContext?.Items[UserResolutionMiddleware.UserIdItemKey] is Guid resolved)
        {
            userId = resolved;
            return true;
        }

        userId = Guid.Empty;
        return false;
    }
}
