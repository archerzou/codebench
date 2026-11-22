using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Web.Api.Authorization;
using Web.Api.Database;
using Web.Api.Features.Users;

namespace IntegrationTests.Authentication;

/// <summary>1.4.1: the first authenticated request creates the local user (learner, or admin for a verified bootstrap email).</summary>
public sealed class UserProvisioningTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private async Task<User?> FindUserAsync(string externalId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Users.AsNoTracking().SingleOrDefaultAsync(u => u.ExternalId == externalId);
    }

    [Fact]
    public async Task FirstRequest_Should_ProvisionLearner_When_TokenCarriesEmail()
    {
        string externalId = UniqueExternalId();
        string email = UniqueEmail();
        Authenticate(TestJwt.Create(externalId, email: email, emailVerified: true));

        GetMe.Response? me = await HttpClient.GetFromJsonAsync<GetMe.Response>("me", TestJson.Options);

        me.ShouldNotBeNull();
        me.Email.ShouldBe(email);
        me.Permissions.ShouldBe(RolePermissionMatrix.ByRole[Role.Learner], ignoreOrder: true);
        User user = (await FindUserAsync(externalId)).ShouldNotBeNull();
        user.Role.ShouldBe(Role.Learner);
    }

    [Fact]
    public async Task FirstRequest_Should_ProvisionAdmin_When_BootstrapEmailIsVerified()
    {
        string externalId = UniqueExternalId();
        Authenticate(TestJwt.Create(externalId, email: TestWebhook.BootstrapEmail.ToUpperInvariant(), emailVerified: true));

        GetMe.Response? me = await HttpClient.GetFromJsonAsync<GetMe.Response>("me", TestJson.Options);

        me.ShouldNotBeNull().Permissions.ShouldBe(RolePermissionMatrix.ByRole[Role.Admin], ignoreOrder: true);
        (await FindUserAsync(externalId)).ShouldNotBeNull().Role.ShouldBe(Role.Admin);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]       // claim missing → unverified (fail closed)
    public async Task FirstRequest_Should_ProvisionLearner_When_BootstrapEmailIsNotVerified(bool? emailVerified)
    {
        string externalId = UniqueExternalId();
        Authenticate(TestJwt.Create(externalId, email: TestWebhook.BootstrapEmail, emailVerified: emailVerified));

        await HttpClient.GetFromJsonAsync<GetMe.Response>("me", TestJson.Options);

        (await FindUserAsync(externalId)).ShouldNotBeNull().Role.ShouldBe(Role.Learner);
    }

    [Fact]
    public async Task FirstRequest_Should_NotProvision_When_TokenHasNoEmail()
    {
        string externalId = UniqueExternalId();
        Authenticate(TestJwt.Create(externalId));

        HttpResponseMessage response = await HttpClient.GetAsync("me");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Users.NotProvisioned");
        (await FindUserAsync(externalId)).ShouldBeNull();
    }

    [Fact]
    public async Task FirstRequest_Should_CreateExactlyOneUser_When_RequestsRace()
    {
        string externalId = UniqueExternalId();
        string token = TestJwt.Create(externalId, email: UniqueEmail(), emailVerified: true);

        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await HttpClient.SendAsync(request);
        }));

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
        using IServiceScope scope = Factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.CountAsync(u => u.ExternalId == externalId)).ShouldBe(1);
    }

    [Fact]
    public async Task Bootstrap_Should_NotPromoteExistingUser_When_EmailMatchesLater()
    {
        User existing = await CreateUserAsync(email: TestWebhook.BootstrapEmail);
        Authenticate(TestJwt.Create(existing.ExternalId, email: TestWebhook.BootstrapEmail, emailVerified: true));

        GetMe.Response? me = await HttpClient.GetFromJsonAsync<GetMe.Response>("me", TestJson.Options);

        me.ShouldNotBeNull().Permissions.ShouldNotContain(Permissions.AdminAccess);
    }
}
