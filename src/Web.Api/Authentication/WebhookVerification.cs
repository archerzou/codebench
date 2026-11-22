namespace Web.Api.Authentication;

public enum WebhookVerification
{
    Valid,
    Invalid,
    /// <summary><c>Clerk:WebhookSecret</c> is empty — the webhook can't be accepted (503) until it is set.</summary>
    NotConfigured
}
