using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Svix;
using Web.Api.Authentication;

namespace Web.Api.UnitTests.Authentication;

public sealed class ClerkWebhookVerifierTests
{
    private static readonly string Secret = "whsec_" + Convert.ToBase64String(Encoding.UTF8.GetBytes("unit-test-webhook-signing-key-0001"));
    private const string Payload = """{"type":"user.created","data":{"id":"user_1"}}""";

    private static ClerkWebhookVerifier Verifier(string secret) =>
        new(Microsoft.Extensions.Options.Options.Create(new ClerkOptions { WebhookSecret = secret }));

    private static HeaderDictionary SignedHeaders(string payload, DateTimeOffset? at = null, string? secret = null)
    {
        DateTimeOffset timestamp = at ?? DateTimeOffset.UtcNow;
        const string MessageId = "msg_unit";
        return new HeaderDictionary
        {
            [ClerkWebhookVerifier.IdHeader] = MessageId,
            [ClerkWebhookVerifier.TimestampHeader] = timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            [ClerkWebhookVerifier.SignatureHeader] = new Webhook(secret ?? Secret).Sign(MessageId, timestamp, payload)
        };
    }

    [Fact]
    public void Verify_Should_ReturnValid_When_SignatureMatches()
    {
        Verifier(Secret).Verify(Payload, SignedHeaders(Payload)).ShouldBe(WebhookVerification.Valid);
    }

    [Fact]
    public void Verify_Should_ReturnInvalid_When_BodyChanged()
    {
        Verifier(Secret).Verify(Payload.Replace("user_1", "user_2", StringComparison.Ordinal), SignedHeaders(Payload))
            .ShouldBe(WebhookVerification.Invalid);
    }

    [Fact]
    public void Verify_Should_ReturnInvalid_When_SignedWithOtherSecret()
    {
        string other = "whsec_" + Convert.ToBase64String(Encoding.UTF8.GetBytes("another-key-entirely-000000000000"));

        Verifier(Secret).Verify(Payload, SignedHeaders(Payload, secret: other)).ShouldBe(WebhookVerification.Invalid);
    }

    [Fact]
    public void Verify_Should_ReturnInvalid_When_TimestampIsOutsideTolerance()
    {
        Verifier(Secret).Verify(Payload, SignedHeaders(Payload, DateTimeOffset.UtcNow.AddMinutes(-30)))
            .ShouldBe(WebhookVerification.Invalid);
    }

    [Theory]
    [InlineData(ClerkWebhookVerifier.IdHeader)]
    [InlineData(ClerkWebhookVerifier.TimestampHeader)]
    [InlineData(ClerkWebhookVerifier.SignatureHeader)]
    public void Verify_Should_ReturnInvalid_When_HeaderIsMissing(string header)
    {
        HeaderDictionary headers = SignedHeaders(Payload);
        headers.Remove(header);

        Verifier(Secret).Verify(Payload, headers).ShouldBe(WebhookVerification.Invalid);
    }

    [Fact]
    public void Verify_Should_ReturnInvalid_When_SignatureIsGarbage()
    {
        HeaderDictionary headers = SignedHeaders(Payload);
        headers[ClerkWebhookVerifier.SignatureHeader] = "v1,not-base64!!";

        Verifier(Secret).Verify(Payload, headers).ShouldBe(WebhookVerification.Invalid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Verify_Should_ReturnNotConfigured_When_SecretIsEmpty(string secret)
    {
        Verifier(secret).Verify(Payload, SignedHeaders(Payload)).ShouldBe(WebhookVerification.NotConfigured);
    }
}
