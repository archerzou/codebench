using System.Net;
using System.Net.Http.Json;
using IntegrationTests.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Web.Api.Authorization;
using Web.Api.Database;
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

        GetMe.Response? me = await HttpClient.GetFromJsonAsync<GetMe.Response>("me", TestJson.Options);

        me.ShouldNotBeNull();
        me.Id.ShouldBe(user.Id);
        me.Email.ShouldBe(user.Email);
        me.Permissions.ShouldBe(["problems:read", "submissions:create", "submissions:read-own"]);
    }

    [Theory]
    [InlineData(Role.Learner)]
    [InlineData(Role.Author)]
    [InlineData(Role.Admin)]
    public async Task GetMe_Should_ListRolePermissionsFromDatabase_When_SignedIn(Role role)
    {
        User user = await CreateUserAsync(role: role);
        Authenticate(TestJwt.Create(user.ExternalId));

        GetMe.Response? me = await HttpClient.GetFromJsonAsync<GetMe.Response>("me", TestJson.Options);

        me.ShouldNotBeNull().Permissions.ShouldBe(RolePermissionMatrix.ByRole[role], ignoreOrder: true);
    }

    [Fact]
    public async Task GetMe_Should_ListNoPermissions_When_UserIsDisabled()
    {
        User user = await CreateUserAsync(role: Role.Admin);
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            User tracked = await db.Users.SingleAsync(u => u.Id == user.Id);
            tracked.Disable();
            await db.SaveChangesAsync();
        }

        Authenticate(TestJwt.Create(user.ExternalId));

        GetMe.Response? me = await HttpClient.GetFromJsonAsync<GetMe.Response>("me", TestJson.Options);

        me.ShouldNotBeNull().Permissions.ShouldBeEmpty();
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
        GetMe.Response? me = await HttpClient.GetFromJsonAsync<GetMe.Response>("me", TestJson.Options);

        me.ShouldNotBeNull();
        me.Id.ShouldBe(user.Id);
    }
}
