using System.Net;
using System.Net.Http.Json;
using IntegrationTests.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Web.Api.Database;
using Web.Api.Features.Users;

namespace IntegrationTests.Users;

/// <summary>1.4.2: <c>POST /webhooks/clerk</c> over real HTTP with real Svix signatures (SC-02).</summary>
public sealed class HandleClerkWebhookTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private async Task<User?> FindUserAsync(string externalId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Users.AsNoTracking().SingleOrDefaultAsync(u => u.ExternalId == externalId);
    }

    private Task<HttpResponseMessage> SendAsync(string body) => SendSignedAsync(body);

    private async Task<HttpResponseMessage> SendSignedAsync(
        string body, string? signedBody = null, DateTimeOffset? timestamp = null, string? secret = null)
    {
        using HttpRequestMessage request = TestWebhook.Request(body, signedBody, timestamp, secret);
        return await HttpClient.SendAsync(request);
    }

    [Fact]
    public async Task HandleClerkWebhook_Should_CreateLearner_When_UserCreated()
    {
        string externalId = UniqueExternalId();
        string email = UniqueEmail();

        HttpResponseMessage response = await SendAsync(TestWebhook.UserEvent(HandleClerkWebhook.UserCreated, externalId, email));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        User user = (await FindUserAsync(externalId)).ShouldNotBeNull();
        user.Email.ShouldBe(email);          // the primary address, not the first one in the list
        user.Role.ShouldBe(Role.Learner);
    }

    [Fact]
    public async Task HandleClerkWebhook_Should_GrantAdmin_When_CreatedWithVerifiedBootstrapEmail()
    {
        string externalId = UniqueExternalId();

        await SendAsync(TestWebhook.UserEvent(HandleClerkWebhook.UserCreated, externalId, TestWebhook.BootstrapEmail));

        (await FindUserAsync(externalId)).ShouldNotBeNull().Role.ShouldBe(Role.Admin);
    }

    [Fact]
    public async Task HandleClerkWebhook_Should_CreateLearner_When_BootstrapEmailIsUnverified()
    {
        string externalId = UniqueExternalId();

        await SendAsync(TestWebhook.UserEvent(HandleClerkWebhook.UserCreated, externalId, TestWebhook.BootstrapEmail, verificationStatus: "unverified"));

        (await FindUserAsync(externalId)).ShouldNotBeNull().Role.ShouldBe(Role.Learner);
    }

    [Fact]
    public async Task HandleClerkWebhook_Should_BeIdempotent_When_DeliveredTwice()
    {
        string externalId = UniqueExternalId();
        string body = TestWebhook.UserEvent(HandleClerkWebhook.UserCreated, externalId, UniqueEmail());

        (await SendAsync(body)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await SendAsync(body)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using IServiceScope scope = Factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.CountAsync(u => u.ExternalId == externalId)).ShouldBe(1);
    }

    [Fact]
    public async Task HandleClerkWebhook_Should_UpdateEmail_When_UserUpdated()
    {
        User user = await CreateUserAsync();
        string newEmail = UniqueEmail();

        HttpResponseMessage response = await SendAsync(TestWebhook.UserEvent(HandleClerkWebhook.UserUpdated, user.ExternalId, newEmail));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await FindUserAsync(user.ExternalId)).ShouldNotBeNull().Email.ShouldBe(newEmail);
    }

    [Fact]
    public async Task HandleClerkWebhook_Should_ProvisionUser_When_UpdatedArrivesBeforeCreated()
    {
        string externalId = UniqueExternalId();

        await SendAsync(TestWebhook.UserEvent(HandleClerkWebhook.UserUpdated, externalId, UniqueEmail()));

        (await FindUserAsync(externalId)).ShouldNotBeNull();
    }

    [Fact]
    public async Task HandleClerkWebhook_Should_SoftDeleteAndRevokeAccess_When_UserDeleted()
    {
        User user = await CreateUserAsync(role: Role.Admin);
        Authenticate(TestJwt.Create(user.ExternalId));
        (await HttpClient.GetFromJsonAsync<GetMe.Response>("me", TestJson.Options)).ShouldNotBeNull().Permissions.ShouldNotBeEmpty();   // primes caches

        HttpResponseMessage response = await SendAsync(TestWebhook.Deleted(user.ExternalId));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        User deleted = (await FindUserAsync(user.ExternalId)).ShouldNotBeNull();
        deleted.DeletedAt.ShouldNotBeNull();
        deleted.IsDisabled.ShouldBeTrue();
        deleted.Email.ShouldNotBe(user.Email);
        (await HttpClient.GetFromJsonAsync<GetMe.Response>("me", TestJson.Options)).ShouldNotBeNull().Permissions.ShouldBeEmpty();   // caches invalidated
    }

    [Fact]
    public async Task HandleClerkWebhook_Should_Return204_When_DeletedUserIsUnknown()
    {
        (await SendAsync(TestWebhook.Deleted(UniqueExternalId()))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task HandleClerkWebhook_Should_IgnoreEvent_When_TypeIsNotHandled()
    {
        string externalId = UniqueExternalId();

        HttpResponseMessage response = await SendAsync(TestWebhook.UserEvent("session.created", externalId, UniqueEmail()));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await FindUserAsync(externalId)).ShouldBeNull();
    }

    [Fact]
    public async Task HandleClerkWebhook_Should_Return400_When_BodyWasTampered()
    {
        string externalId = UniqueExternalId();
        string signed = TestWebhook.UserEvent(HandleClerkWebhook.UserCreated, externalId, UniqueEmail());
        string tampered = TestWebhook.UserEvent(HandleClerkWebhook.UserCreated, externalId, TestWebhook.BootstrapEmail);

        HttpResponseMessage response = await SendSignedAsync(tampered, signedBody: signed);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Users.InvalidWebhookSignature");
        (await FindUserAsync(externalId)).ShouldBeNull();
    }

    [Fact]
    public async Task HandleClerkWebhook_Should_Return400_When_SignedWithAnotherSecret()
    {
        string otherSecret = "whsec_" + Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        string body = TestWebhook.UserEvent(HandleClerkWebhook.UserCreated, UniqueExternalId(), UniqueEmail());

        (await SendSignedAsync(body, secret: otherSecret)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task HandleClerkWebhook_Should_Return400_When_TimestampIsStale()
    {
        string body = TestWebhook.UserEvent(HandleClerkWebhook.UserCreated, UniqueExternalId(), UniqueEmail());

        HttpResponseMessage response = await SendSignedAsync(body, timestamp: DateTimeOffset.UtcNow.AddHours(-1));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task HandleClerkWebhook_Should_Return400_When_SignatureHeadersAreMissing()
    {
        string body = TestWebhook.UserEvent(HandleClerkWebhook.UserCreated, UniqueExternalId(), UniqueEmail());

        using var content = new StringContent(body);
        HttpResponseMessage response = await HttpClient.PostAsync("webhooks/clerk", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task HandleClerkWebhook_Should_Return400_When_SignedPayloadIsMalformed()
    {
        (await SendAsync("""{"type":"user.created","data":{}}""")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync("not json")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task HandleClerkWebhook_Should_Return400_When_EmailIsInvalid()
    {
        HttpResponseMessage response = await SendAsync(TestWebhook.UserEvent(HandleClerkWebhook.UserCreated, UniqueExternalId(), "not-an-email"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);   // ValidationDecorator
    }
}
