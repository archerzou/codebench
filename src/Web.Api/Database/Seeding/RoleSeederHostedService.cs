namespace Web.Api.Database.Seeding;

/// <summary>
/// Runs <see cref="RoleSeeder"/> once during host start, before requests are served, so permission checks
/// never see an empty matrix. Fails start-up with a clear message if the database hasn't been migrated
/// (Production applies migrations explicitly with <c>--migrate</c> — DM-06).
/// </summary>
internal sealed class RoleSeederHostedService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        RoleSeeder seeder = scope.ServiceProvider.GetRequiredService<RoleSeeder>();

        try
        {
            await seeder.SeedAsync(cancellationToken);
        }
        catch (Exception exception) when (RoleSeeder.IsMissingSchema(exception))
        {
            throw new InvalidOperationException(
                "The database schema is missing (table role_permissions). Apply migrations first: " +
                "`docker compose run --rm api --migrate` (Production) or start in Development.",
                exception);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
