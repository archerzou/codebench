using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Web.Api.Authorization;
using Web.Api.Database;
using Web.Api.Database.Seeding;
using Web.Api.Features.Users;

namespace IntegrationTests.Database.Seeding;

/// <summary>
/// RoleSeeder against real Postgres (its insert is raw SQL with <c>unnest … ON CONFLICT</c>). Tests in the
/// integration collection run sequentially, and every test leaves the matrix complete again.
/// </summary>
public sealed class RoleSeederTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private async Task<(Role Role, string Permission)[]> ReadTableAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return [.. (await db.RolePermissions.AsNoTracking().ToListAsync()).Select(rp => (rp.Role, rp.Permission))];
    }

    private async Task<RoleSeeder.SeedResult> SeedAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RoleSeeder>().SeedAsync(CancellationToken.None);
    }

    private async Task ExecuteAsync(FormattableString sql)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlAsync(sql);
    }

    [Fact]
    public async Task Startup_Should_WriteTheFullMatrix_When_HostStarts()
    {
        (Role Role, string Permission)[] table = await ReadTableAsync();

        table.ShouldBe(RolePermissionMatrix.Rows, ignoreOrder: true);
        table.Length.ShouldBe(3 + 6 + 11);
    }

    [Fact]
    public async Task SeedAsync_Should_ChangeNothing_When_MatrixIsAlreadyWritten()
    {
        RoleSeeder.SeedResult result = await SeedAsync();

        result.ShouldBe(new RoleSeeder.SeedResult(Added: 0, Removed: 0));
        result.Changed.ShouldBeFalse();
    }

    [Fact]
    public async Task SeedAsync_Should_RepairTable_When_RowsAreMissingOrStale()
    {
        await ExecuteAsync($"DELETE FROM role_permissions WHERE role = 'Admin' AND permission = 'stats:read'");
        await ExecuteAsync($"INSERT INTO role_permissions (role, permission) VALUES ('Learner', 'users:manage')");

        RoleSeeder.SeedResult result = await SeedAsync();

        result.ShouldBe(new RoleSeeder.SeedResult(Added: 1, Removed: 1));
        (await ReadTableAsync()).ShouldBe(RolePermissionMatrix.Rows, ignoreOrder: true);
    }

    [Fact]
    public async Task SeedAsync_Should_InvalidateCachedPermissions_When_MatrixChanged()
    {
        await SignInAsNewUserAsync();
        await ExecuteAsync($"DELETE FROM role_permissions WHERE role = 'Learner' AND permission = 'problems:read'");

        // Cached for 5 minutes with the row missing.
        GetMe.Response? before = await HttpClient.GetFromJsonAsync<GetMe.Response>("me", TestJson.Options);
        before.ShouldNotBeNull().Permissions.ShouldNotContain(Permissions.ProblemsRead);

        await SeedAsync();   // re-adds the row and clears the "permissions" tag

        GetMe.Response? after = await HttpClient.GetFromJsonAsync<GetMe.Response>("me", TestJson.Options);
        after.ShouldNotBeNull().Permissions.ShouldContain(Permissions.ProblemsRead);
    }
}
