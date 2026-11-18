using Web.Api.Common;

namespace Web.Api.Features.Users;

public static class UserErrors
{
    public static Error NotFound(Guid userId) => Error.NotFound(
        "Users.NotFound",
        $"The user with the Id = '{userId}' was not found");

    /// <summary>Signed in with Clerk, but no local account exists yet (created on first request, step 1.4).</summary>
    public static readonly Error NotProvisioned = Error.NotFound(
        "Users.NotProvisioned",
        "Your account has not been set up yet. Sign out and sign in again.");
}
