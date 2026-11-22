using Microsoft.Extensions.Caching.Hybrid;
using Web.Api.Authorization;
using Web.Api.Database;
using Web.Api.Features.Users;
using Web.Api.UnitTests.Abstractions;

namespace Web.Api.UnitTests.Authorization;

public sealed class PermissionProviderTests : BaseHandlerTest
{
    private static readonly DateTime CreatedAt = new(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc);

    private static async Task<User> AddUserAsync(ApplicationDbContext db, string externalId, Role role, bool disabled = false)
    {
        var user = User.Create(externalId, $"{externalId}@example.com", role, CreatedAt);
        if (disabled)
        {
            user.Disable();
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user;
    }

    private static async Task SeedMatrixAsync(ApplicationDbContext db)
    {
        db.RolePermissions.AddRange(RolePermissionMatrix.Rows.Select(row => RolePermission.Create(row.Role, row.Permission)));
        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData(Role.Learner)]
    [InlineData(Role.Author)]
    [InlineData(Role.Admin)]
    public async Task GetForExternalIdAsync_Should_ReturnRolePermissions_When_UserIsActive(Role role)
    {
        using ApplicationDbContext db = CreateDbContext();
        await SeedMatrixAsync(db);
        await AddUserAsync(db, "user_active", role);
        var provider = new PermissionProvider(db, CreateCache());

        IReadOnlySet<string> permissions = await provider.GetForExternalIdAsync("user_active");

        permissions.ShouldBe(RolePermissionMatrix.ByRole[role], ignoreOrder: true);
    }

    [Fact]
    public async Task GetForExternalIdAsync_Should_ReturnNone_When_UserIsDisabled()
    {
        using ApplicationDbContext db = CreateDbContext();
        await SeedMatrixAsync(db);
        await AddUserAsync(db, "user_disabled", Role.Admin, disabled: true);
        var provider = new PermissionProvider(db, CreateCache());

        IReadOnlySet<string> permissions = await provider.GetForExternalIdAsync("user_disabled");

        permissions.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetForExternalIdAsync_Should_NotCacheUnknownUser_When_ProvisionedLater()
    {
        using ApplicationDbContext db = CreateDbContext();
        await SeedMatrixAsync(db);
        var provider = new PermissionProvider(db, CreateCache());

        (await provider.GetForExternalIdAsync("user_later")).ShouldBeEmpty();

        await AddUserAsync(db, "user_later", Role.Learner);

        (await provider.GetForExternalIdAsync("user_later")).ShouldBe(RolePermissionMatrix.ByRole[Role.Learner], ignoreOrder: true);
    }

    [Fact]
    public async Task GetForExternalIdAsync_Should_ServeCachedSet_When_RoleChangesUntilKeyIsRemoved()
    {
        using ApplicationDbContext db = CreateDbContext();
        await SeedMatrixAsync(db);
        User user = await AddUserAsync(db, "user_promoted", Role.Learner);
        HybridCache cache = CreateCache();
        var provider = new PermissionProvider(db, cache);
        (await provider.GetForExternalIdAsync("user_promoted")).ShouldNotContain(Permissions.AdminAccess);

        user.ChangeRole(Role.Admin);
        await db.SaveChangesAsync();

        // Cached for 5 minutes: callers that change a role must remove the key (QY-05).
        (await provider.GetForExternalIdAsync("user_promoted")).ShouldNotContain(Permissions.AdminAccess);

        await cache.RemoveAsync(PermissionCacheKeys.ForUser("user_promoted"));
        (await provider.GetForExternalIdAsync("user_promoted")).ShouldContain(Permissions.AdminAccess);
    }
}
