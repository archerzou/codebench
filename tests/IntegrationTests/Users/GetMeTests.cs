using System.Net;
using System.Net.Http.Json;
using IntegrationTests.Authentication;
using Web.Api.Features.Users;

namespace IntegrationTests.Users;

public sealed class GetMeTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task GetMe_Should_Return401_When_Anonymous()
    {
        HttpResponseMessage response = await HttpClient.GetAsync("me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_Should_ReturnAccount_When_SignedIn()
    {
        User user = await SignInAsNewUserAsync();

        GetMe.Response? me = await HttpClient.GetFromJsonAsync<GetMe.Response>("me");

        me.ShouldNotBeNull();
        me.Id.ShouldBe(user.Id);
        me.Email.ShouldBe(user.Email);
        me.Permissions.ShouldBeEmpty();     // role matrix arrives in step 1.3
    }

    [Fact]
    public async Task GetMe_Should_Return404NotProvisioned_When_NoLocalUser()
    {
        Authenticate(TestJwt.Create(UniqueExternalId()));

        HttpResponseMessage response = await HttpClient.GetAsync("me");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Users.NotProvisioned");
    }

    [Fact]
    public async Task GetMe_Should_FindUser_When_ProvisionedAfterFirstRequest()
    {
        // A miss must not be cached under users:ext:{sub}, or a just-created account stays invisible.
        string externalId = UniqueExternalId();
        Authenticate(TestJwt.Create(externalId));
        (await HttpClient.GetAsync("me")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        User user = await CreateUserAsync(externalId);
        GetMe.Response? me = await HttpClient.GetFromJsonAsync<GetMe.Response>("me");

        me.ShouldNotBeNull();
        me.Id.ShouldBe(user.Id);
    }
}
