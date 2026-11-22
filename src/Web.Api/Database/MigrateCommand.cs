using Microsoft.EntityFrameworkCore;

namespace Web.Api.Database;

/// <summary>
/// <c>--migrate</c>: applies pending EF Core migrations, then exits without starting the web server — how
/// Production migrates (DM-06; <c>docker compose run --rm api --migrate</c>, run by <c>deploy.sh</c> before
/// <c>up -d</c>). Development keeps applying migrations at startup. Combine with <c>--seed</c> to migrate and
/// then seed in one run. Exit codes: 0 ok, 1 migration failed.
/// </summary>
internal static class MigrateCommand
{
    public const string Flag = "--migrate";

    public static bool IsRequested(IReadOnlyCollection<string> args) => args.Contains(Flag);

    public static async Task<int> RunAsync(WebApplication app)
    {
        ILogger logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(MigrateCommand));

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            string[] pending = [.. await db.Database.GetPendingMigrationsAsync(timeout.Token)];

            if (pending.Length == 0)
            {
                logger.LogInformation("{Flag}: the database schema is up to date", Flag);
                return 0;
            }

            logger.LogInformation("{Flag}: applying {Count} migration(s): {Migrations}", Flag, pending.Length, pending);
            await db.Database.MigrateAsync(timeout.Token);
            logger.LogInformation("{Flag}: done", Flag);

            return 0;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "{Flag} failed (is the database reachable?)", Flag);
            return 1;
        }
    }
}
