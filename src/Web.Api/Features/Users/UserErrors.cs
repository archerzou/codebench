using Web.Api.Common;

namespace Web.Api.Features.Users;

public static class UserErrors
{
    public static Error NotFound(Guid userId) => Error.NotFound(
        "Users.NotFound",
        $"The user with the Id = '{userId}' was not found");

    public static Error Deleted(Guid userId) => Error.Conflict(
        "Users.Deleted",
        $"The user with the Id = '{userId}' was deleted in Clerk and cannot be re-enabled");

    public static readonly Error InvalidWebhookSignature = Error.Problem(
        "Users.InvalidWebhookSignature",
        "The webhook signature is missing, stale or does not match.");

    public static readonly Error InvalidWebhookPayload = Error.Problem(
        "Users.InvalidWebhookPayload",
        "The webhook body is not a Clerk event with a type and a user id.");

    /// <summary>503: Svix retries, so events delivered before the secret is configured are not lost.</summary>
    public static readonly Error WebhookNotConfigured = Error.Unavailable(
        "Users.WebhookNotConfigured",
        "Clerk:WebhookSecret is not configured; the webhook cannot be verified.");

    /// <summary>Signed in with Clerk, but no local account exists yet (created on first request, step 1.4).</summary>
    public static readonly Error NotProvisioned = Error.NotFound(
        "Users.NotProvisioned",
        "Your account has not been set up yet. Sign out and sign in again.");
}
