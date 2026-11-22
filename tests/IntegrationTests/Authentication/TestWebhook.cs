using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Svix;

namespace IntegrationTests.Authentication;

/// <summary>Builds and Svix-signs Clerk-shaped webhook requests for integration tests.</summary>
internal static class TestWebhook
{
    public static readonly string Secret = "whsec_" + Convert.ToBase64String(Encoding.UTF8.GetBytes("codebench-integration-webhook-key!"));

    public const string BootstrapEmail = "bootstrap-admin@codebench.test";

    public static string UserEvent(string type, string externalId, string? email, string verificationStatus = "verified") =>
        JsonSerializer.Serialize(new
        {
            type,
            @object = "event",
            timestamp = 1_763_600_000_000,
            instance_id = "ins_test",
            data = new
            {
                id = externalId,
                @object = "user",
                primary_email_address_id = email is null ? null : "idn_primary",
                email_addresses = email is null
                    ? Array.Empty<object>()
                    : [
                        new { id = "idn_other", email_address = "other@example.com", verification = new { status = "verified", strategy = "ticket" } },
                        new { id = "idn_primary", email_address = email, verification = new { status = verificationStatus, strategy = "email_code" } }
                    ]
            }
        });

    public static string Deleted(string externalId) =>
        JsonSerializer.Serialize(new { type = "user.deleted", @object = "event", data = new { id = externalId, @object = "user", deleted = true } });

    /// <summary>A signed request; <paramref name="signedBody"/> lets a test sign one body and send another.</summary>
    public static HttpRequestMessage Request(string body, string? signedBody = null, DateTimeOffset? timestamp = null, string? secret = null)
    {
        string messageId = $"msg_{Guid.NewGuid():N}";
        DateTimeOffset at = timestamp ?? DateTimeOffset.UtcNow;
        string signature = new Webhook(secret ?? Secret).Sign(messageId, at, signedBody ?? body);

        var request = new HttpRequestMessage(HttpMethod.Post, "webhooks/clerk")
        {
            Content = new StringContent(body, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
        };
        request.Headers.Add("svix-id", messageId);
        request.Headers.Add("svix-timestamp", at.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.Add("svix-signature", signature);

        return request;
    }
}
