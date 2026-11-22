using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Web.Api.Authorization;
using Web.Api.Database;
using Web.Api.Features.Users;

namespace Web.Api.UnitTests.Authorization;

/// <summary>
/// The handler behind <c>.HasPermission(...)</c>, wired with real DI (scoped DbContext, HybridCache,
/// PermissionProvider) over an in-memory database — the same object graph the API resolves.
/// </summary>
public sealed class PermissionAuthorizationHandlerTests : IDisposable
{
    private static readonly DateTime CreatedAt = new(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc);

    private readonly ServiceProvider _services;

    public PermissionAuthorizationHandlerTests()
    {
        string database = $"authz-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IDomainEventsDispatcher>());
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(database));
#pragma warning disable EXTEXP0018 // HybridCache is released; the API is stable in .NET 10 (same as the API's registration).
        services.AddHybridCache();
#pragma warning restore EXTEXP0018
        services.AddScoped<PermissionProvider>();
        _services = services.BuildServiceProvider();
    }

    public void Dispose() => _services.Dispose();

    private async Task AddUserAsync(string externalId, Role role, bool disabled = false)
    {
        using IServiceScope scope = _services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await db.RolePermissions.AnyAsync())
        {
            db.RolePermissions.AddRange(RolePermissionMatrix.Rows.Select(row => RolePermission.Create(row.Role, row.Permission)));
        }

        var user = User.Create(externalId, $"{externalId}@example.com", role, CreatedAt);
        if (disabled)
        {
            user.Disable();
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    private async Task<bool> IsAuthorizedAsync(ClaimsPrincipal principal, string permission)
    {
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], principal, resource: null);
        var handler = new PermissionAuthorizationHandler(_services.GetRequiredService<IServiceScopeFactory>());

        await handler.HandleAsync(context);

        return context.HasSucceeded;
    }

    private static ClaimsPrincipal SignedIn(string externalId) =>
        new(new ClaimsIdentity([new Claim("sub", externalId)], authenticationType: "Test", nameType: "sub", roleType: null));

    [Fact]
    public async Task HandleAsync_Should_Succeed_When_RoleGrantsPermission()
    {
        await AddUserAsync("user_admin", Role.Admin);

        (await IsAuthorizedAsync(SignedIn("user_admin"), Permissions.ProblemsPublish)).ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_Should_Deny_When_RoleLacksPermission()
    {
        await AddUserAsync("user_author", Role.Author);

        // Guide 09: an author edits but cannot publish.
        (await IsAuthorizedAsync(SignedIn("user_author"), Permissions.ProblemsWrite)).ShouldBeTrue();
        (await IsAuthorizedAsync(SignedIn("user_author"), Permissions.ProblemsPublish)).ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_Should_Deny_When_UserIsDisabled()
    {
        await AddUserAsync("user_disabled", Role.Admin, disabled: true);

        (await IsAuthorizedAsync(SignedIn("user_disabled"), Permissions.AdminAccess)).ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_Should_Deny_When_UserIsUnknown()
    {
        (await IsAuthorizedAsync(SignedIn("user_nobody"), Permissions.ProblemsRead)).ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_Should_Deny_When_Anonymous()
    {
        await AddUserAsync("user_learner", Role.Learner);

        (await IsAuthorizedAsync(new ClaimsPrincipal(new ClaimsIdentity()), Permissions.ProblemsRead)).ShouldBeFalse();
    }
}
