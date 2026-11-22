using System.Net;
using Microsoft.Extensions.Options;
using Svix;
using Svix.Exceptions;

namespace Web.Api.Authentication;

/// <summary>
/// Verifies Clerk (Svix) webhook signatures over the <b>raw</b> body: HMAC-SHA256 of
/// <c>{svix-id}.{svix-timestamp}.{body}</c> with the <c>whsec_…</c> secret, timestamp within Svix's tolerance
/// (SC-02). Nothing is parsed before this returns <see cref="WebhookVerification.Valid"/>.
/// </summary>
internal sealed class ClerkWebhookVerifier(IOptions<ClerkOptions> clerkOptions)
{
    internal const string IdHeader = "svix-id";
    internal const string TimestampHeader = "svix-timestamp";
    internal const string SignatureHeader = "svix-signature";

    public WebhookVerification Verify(string payload, IHeaderDictionary headers)
    {
        string secret = clerkOptions.Value.WebhookSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            return WebhookVerification.NotConfigured;
        }

        var svixHeaders = new WebHeaderCollection();
        foreach (string name in (string[])[IdHeader, TimestampHeader, SignatureHeader])
        {
            string? value = headers[name];
            if (string.IsNullOrWhiteSpace(value))
            {
                return WebhookVerification.Invalid;
            }

            svixHeaders.Add(name, value);
        }

        try
        {
            new Webhook(secret).Verify(payload, svixHeaders);
            return WebhookVerification.Valid;
        }
        catch (WebhookVerificationException)
        {
            return WebhookVerification.Invalid;
        }
        catch (FormatException)
        {
            // Malformed secret or signature encoding: treat as a failed verification, never as success.
            return WebhookVerification.Invalid;
        }
    }
}
