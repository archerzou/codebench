using System.Net.Http.Headers;
using IntegrationTests.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Web.Api.Common;
using Web.Api.Database;
using Web.Api.Features.Users;

namespace IntegrationTests;

[Collection(nameof(IntegrationTestCollection))]
public abstract class BaseIntegrationTest
{
    protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
    {
        Factory = factory;
        HttpClient = factory.CreateClient();
    }

    protected IntegrationTestWebAppFactory Factory { get; }

    protected HttpClient HttpClient { get; }

    protected static string UniqueEmail() => $"test-{Guid.NewGuid():N}@example.com";

    /// <summary>A fresh Clerk-style user id per test (TS-05).</summary>
    protected static string UniqueExternalId() => $"user_test_{Guid.NewGuid():N}";

    /// <summary>Inserts a local user, as the webhook / first-request upsert will (step 1.4).</summary>
    protected async Task<User> CreateUserAsync(string? externalId = null, string? email = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IDateTimeProvider clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var user = User.Create(externalId ?? UniqueExternalId(), email ?? UniqueEmail(), clock.UtcNow);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user;
    }

    /// <summary>Creates a local user and authenticates the client with a valid token for them.</summary>
    protected async Task<User> SignInAsNewUserAsync()
    {
        User user = await CreateUserAsync();
        Authenticate(TestJwt.Create(user.ExternalId));

        return user;
    }

    protected void Authenticate(string accessToken)
    {
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }
}
