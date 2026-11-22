namespace Web.Api.Database.Seeding;

/// <summary>
/// <c>--seed</c>: writes the role matrix and the problems from <c>problems/</c>, then exits without starting
/// the web server (<c>docker compose run --rm api --seed</c>, or <c>dotnet run --project src/Web.Api -- --seed</c>).
/// <c>--seed-demo</c> additionally seeds demo learners — Development only (SC-05); the demo seeder itself
/// arrives in build plan 3A.7. Exit codes: 0 ok, 1 seeding failed, 2 refused.
/// </summary>
internal static class SeedCommand
{
    public const string SeedFlag = "--seed";
    public const string SeedDemoFlag = "--seed-demo";

    public static bool IsRequested(IReadOnlyCollection<string> args) => args.Contains(SeedFlag) || args.Contains(SeedDemoFlag);

    public static async Task<int> RunAsync(WebApplication app, IReadOnlyCollection<string> args)
    {
        ILogger logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SeedCommand));
        bool demo = args.Contains(SeedDemoFlag);

        if (demo && !app.Environment.IsDevelopment())
        {
            logger.LogError("{Flag} refused: demo data is only seeded when ASPNETCORE_ENVIRONMENT=Development", SeedDemoFlag);
            return 2;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();

        try
        {
            await scope.ServiceProvider.GetRequiredService<RoleSeeder>().SeedAsync(timeout.Token);

            string folder = ProblemSeeder.ResolveFolder(app.Configuration, app.Environment);
            await scope.ServiceProvider.GetRequiredService<ProblemSeeder>().SeedAsync(folder, timeout.Token);
        }
        catch (Exception exception) when (exception is ProblemDefinitionException or DirectoryNotFoundException)
        {
            logger.LogError(exception, "Seeding failed: {Reason}", exception.Message);
            return 1;
        }
        catch (Exception exception) when (RoleSeeder.IsMissingSchema(exception))
        {
            logger.LogError(exception, "Seeding failed: the database schema is missing. Run with --migrate first (Production) or start in Development");
            return 1;
        }
#pragma warning disable CA1031 // Top-level CLI boundary: every failure must map to the documented exit code 1, not a crash code.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            logger.LogError(exception, "Seeding failed unexpectedly (is the database reachable?)");
            return 1;
        }

        if (demo)
        {
            logger.LogWarning("{Flag}: the demo data seeder arrives in build plan 3A.7 — no demo data written", SeedDemoFlag);
        }

        return 0;
    }
}
