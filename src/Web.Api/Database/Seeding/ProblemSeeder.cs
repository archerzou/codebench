using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Npgsql;
using Web.Api.Common;
using Web.Api.Features.Problems;

namespace Web.Api.Database.Seeding;

/// <summary>
/// Loads <c>problems/*/problem.yaml</c> into the database — <b>insert-only</b>: an existing slug is skipped,
/// because after seeding the database (admin console) owns the content (ADR 0005). Every file is parsed and
/// validated first, so one broken file seeds nothing. Problems whose grader isn't built yet are deferred
/// (left for a later <c>--seed</c>). Seeds go straight to Published v1.
/// </summary>
internal sealed class ProblemSeeder(
    ApplicationDbContext db,
    IDateTimeProvider dateTimeProvider,
    HybridCache cache,
    ILogger<ProblemSeeder> logger)
{
    /// <summary>Graders that exist. Phase 5 adds Database, Phase 6 Refactoring and the rest.</summary>
    internal static readonly IReadOnlySet<GraderKind> ImplementedGraders = new HashSet<GraderKind> { GraderKind.Algorithms };

    public sealed record SeedResult(IReadOnlyList<string> Inserted, IReadOnlyList<string> Skipped, IReadOnlyList<string> Deferred);

    public async Task<SeedResult> SeedAsync(string folder, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProblemDefinition> definitions = await ReadAllAsync(folder, cancellationToken);

        List<string> inserted = [], skipped = [], deferred = [];
        foreach (ProblemDefinition definition in definitions)
        {
            if (!ImplementedGraders.Contains(definition.GraderKind))
            {
                deferred.Add(definition.Slug);
            }
            else if (await db.Problems.AnyAsync(p => p.Slug == definition.Slug, cancellationToken)
                     || !await TryInsertAsync(definition, cancellationToken))
            {
                skipped.Add(definition.Slug);
            }
            else
            {
                inserted.Add(definition.Slug);
            }
        }

        if (inserted.Count > 0)
        {
            await cache.RemoveByTagAsync(ProblemCacheKeys.Tag, cancellationToken);
        }

        logger.LogInformation(
            "Problems seeded from {Folder}: {Inserted} inserted [{InsertedSlugs}], {Skipped} already present, {Deferred} deferred until their grader exists [{DeferredSlugs}]",
            folder, inserted.Count, string.Join(", ", inserted), skipped.Count, deferred.Count, string.Join(", ", deferred));

        return new SeedResult(inserted, skipped, deferred);
    }

    /// <summary><c>Seeding:ProblemsPath</c>, else <c>/problems</c> (container mount), else <c>{repo}/problems</c> (host run).</summary>
    public static string ResolveFolder(IConfiguration configuration, IHostEnvironment environment)
    {
        string? configured = configuration["Seeding:ProblemsPath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured, environment.ContentRootPath);
        }

        const string ContainerMount = "/problems";
        return Directory.Exists(ContainerMount)
            ? ContainerMount
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "problems"));
    }

    private static async Task<IReadOnlyList<ProblemDefinition>> ReadAllAsync(string folder, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException($"Problem seed folder '{folder}' does not exist (set Seeding:ProblemsPath).");
        }

        List<ProblemDefinition> definitions = [];
        foreach (string file in Directory.EnumerateFiles(folder, "problem.yaml", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            definitions.Add(ProblemDefinitionReader.Read(await File.ReadAllTextAsync(file, cancellationToken), file));
        }

        string? duplicate = definitions.GroupBy(d => d.Slug, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1)?.Key;
        if (duplicate is not null)
        {
            throw ProblemDefinitionException.For(folder, $"slug '{duplicate}' is defined by more than one problem.yaml");
        }

        return definitions;
    }

    /// <summary>One transaction per problem (problem + its tests). False when another seeder inserted it first.</summary>
    private async Task<bool> TryInsertAsync(ProblemDefinition definition, CancellationToken cancellationToken)
    {
        DateTime now = dateTimeProvider.UtcNow;
        var problem = Problem.CreateDraft(definition.ToContent(), now);
        Result published = problem.Publish(now);
        if (published.IsFailure)
        {
            throw new InvalidOperationException($"A new draft could not be published: {published.Error.Code}");
        }

        db.Problems.Add(problem);
        db.TestCases.AddRange(definition.ToTestCases(problem.Id));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }
}
