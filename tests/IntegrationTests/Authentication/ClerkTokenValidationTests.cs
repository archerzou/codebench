using System.Net;
using Web.Api.Features.Users;

namespace IntegrationTests.Authentication;

/// <summary>
/// SC-02: Clerk tokens are accepted only with our issuer, a trusted signature, a valid lifetime and an
/// authorized party. Exercised against the protected <c>GET /me</c> endpoint.
/// </summary>
public sealed class ClerkTokenValidationTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Request_Should_Succeed_When_TokenIsValid()
    {
        User user = await CreateUserAsync();
        Authenticate(TestJwt.Create(user.ExternalId));

        HttpResponseMessage response = await HttpClient.GetAsync("me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Request_Should_Succeed_When_AuthorizedPartyHasTrailingSlash()
    {
        User user = await CreateUserAsync();
        Authenticate(TestJwt.Create(user.ExternalId, authorizedParty: TestJwt.AuthorizedParty + "/"));

        HttpResponseMessage response = await HttpClient.GetAsync("me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Request_Should_Return401_When_AuthorizedPartyIsNotAllowed()
    {
        User user = await CreateUserAsync();
        Authenticate(TestJwt.Create(user.ExternalId, authorizedParty: "https://someone-else.example"));

        HttpResponseMessage response = await HttpClient.GetAsync("me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_Should_Return401_When_AuthorizedPartyIsMissing()
    {
        User user = await CreateUserAsync();
        Authenticate(TestJwt.Create(user.ExternalId, authorizedParty: null));

        HttpResponseMessage response = await HttpClient.GetAsync("me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_Should_Return401_When_IssuerIsWrong()
    {
        User user = await CreateUserAsync();
        Authenticate(TestJwt.Create(user.ExternalId, issuer: "https://other-instance.clerk.accounts.dev"));

        HttpResponseMessage response = await HttpClient.GetAsync("me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_Should_Return401_When_TokenIsExpired()
    {
        User user = await CreateUserAsync();
        Authenticate(TestJwt.Create(user.ExternalId, expiresAt: DateTime.UtcNow.AddMinutes(-5)));

        HttpResponseMessage response = await HttpClient.GetAsync("me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_Should_Return401_When_SignedWithUntrustedKey()
    {
        User user = await CreateUserAsync();
        Authenticate(TestJwt.Create(user.ExternalId, signingKey: TestJwt.CreateUntrustedKey()));

        HttpResponseMessage response = await HttpClient.GetAsync("me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
