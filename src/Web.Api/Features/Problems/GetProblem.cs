using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Web.Api.Authentication;
using Web.Api.Authorization;
using Web.Api.Common;
using Web.Api.Common.Endpoints;
using Web.Api.Common.Extensions;
using Web.Api.Common.Messaging;
using Web.Api.Database;
using Web.Api.Features.Users;

namespace Web.Api.Features.Problems;

/// <summary>
/// <c>GET /problems/{slug}</c> — what the editor needs: statement, starter code, signature, limits and the
/// <b>visible</b> tests; hidden tests only as a count. The response type has no field that could carry the
/// reference solution or hidden test data (QY-04). Published only; a Pro (non-free) problem is visible to
/// Pro users and staff (<c>admin:access</c>), and is <c>404</c> for everyone else — existence isn't leaked (EP-02).
/// The caller-independent detail is cached 10 min under <c>problems:{slug}</c>; the Pro check runs per request.
/// </summary>
public static class GetProblem
{

    public sealed record Query(string Slug) : IQuery<Response>;

    public sealed record Response
    {
        public required Guid Id { get; init; }

        public required string Slug { get; init; }

        public required string Title { get; init; }

        public required Track Track { get; init; }

        public required Difficulty Difficulty { get; init; }

        public required bool IsFree { get; init; }

        public required int Version { get; init; }

        public required string Signature { get; init; }

        public required string StatementMarkdown { get; init; }

        public required string StarterCode { get; init; }

        public required RunLimits Limits { get; init; }

        public required IReadOnlyList<VisibleTest> VisibleTests { get; init; }

        public required int HiddenTestCount { get; init; }

        /// <summary>Database grader: the plan rules the solution must satisfy (shown up front, not secret).</summary>
        public JsonElement? PlanRules { get; init; }

        /// <summary>Refactoring grader: the metric thresholds (shown up front, not secret).</summary>
        public JsonElement? Metrics { get; init; }

        public sealed record RunLimits(int TimeoutSeconds, int MemoryMb);

        public sealed record VisibleTest
        {
            public required string Name { get; init; }

            public required JsonElement Args { get; init; }

            public JsonElement? Expected { get; init; }

            public string? ExpectedException { get; init; }

            public required CompareMode Compare { get; init; }

            public int? TimeBudgetMs { get; init; }

            public long? AllocBudgetBytes { get; init; }
        }
    }

    internal sealed class Handler(
        ApplicationDbContext db,
        HybridCache cache,
        IUserContext userContext,
        PermissionProvider permissionProvider)
        : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            string key = ProblemCacheKeys.BySlug(query.Slug);

            Response? problem = await cache.GetOrCreateAsync(
                key,
                (db, query.Slug),
                static async (state, ct) => await LoadAsync(state.db, state.Slug, ct),
                ProblemCacheKeys.EntryOptions,
                ProblemCacheKeys.Tags,
                cancellationToken);

            if (problem is null)
            {
                await cache.RemoveAsync(key, cancellationToken);   // don't remember a miss; it may be published soon
                return Result.Failure<Response>(ProblemErrors.NotFound(query.Slug));
            }

            if (!problem.IsFree && !await CanOpenProAsync(cancellationToken))
            {
                return Result.Failure<Response>(ProblemErrors.NotFound(query.Slug));
            }

            return problem;
        }

        private async Task<bool> CanOpenProAsync(CancellationToken cancellationToken)
        {
            if (!userContext.IsAuthenticated || !userContext.TryGetUserId(out Guid userId))
            {
                return false;
            }

            IReadOnlySet<string> permissions = await permissionProvider.GetForExternalIdAsync(userContext.ExternalId, cancellationToken);
            if (permissions.Contains(Permissions.AdminAccess))
            {
                return true;   // authors and admins preview the content they manage
            }

            return await db.Users.AnyAsync(u => u.Id == userId && u.Plan == Plan.Pro && !u.IsDisabled, cancellationToken);
        }

        private static async Task<Response?> LoadAsync(ApplicationDbContext db, string slug, CancellationToken cancellationToken)
        {
            // Explicit projection: ReferenceSolution and hidden tests are never selected (QY-01, QY-04).
            var problem = await db.Problems.AsNoTracking()
                .Where(p => p.Slug == slug && p.Status == ProblemStatus.Published)
                .Select(p => new
                {
                    p.Id, p.Slug, p.Title, p.Track, p.Difficulty, p.IsFree, p.Version, p.Signature,
                    p.StatementMarkdown, p.StarterCode, p.Limits.TimeoutSeconds, p.Limits.MemoryMb,
                    p.PlanRulesJson, p.MetricsJson
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (problem is null)
            {
                return null;
            }

            var visible = await db.TestCases.AsNoTracking()
                .Where(t => t.ProblemId == problem.Id && !t.IsHidden)
                .OrderBy(t => t.Order)
                .Select(t => new { t.Name, t.ArgsJson, t.ExpectedJson, t.ExpectedException, t.Compare, t.TimeBudgetMs, t.AllocBudgetBytes })
                .ToListAsync(cancellationToken);

            int hiddenCount = await db.TestCases.CountAsync(t => t.ProblemId == problem.Id && t.IsHidden, cancellationToken);

            return new Response
            {
                Id = problem.Id,
                Slug = problem.Slug,
                Title = problem.Title,
                Track = problem.Track,
                Difficulty = problem.Difficulty,
                IsFree = problem.IsFree,
                Version = problem.Version,
                Signature = problem.Signature,
                StatementMarkdown = problem.StatementMarkdown,
                StarterCode = problem.StarterCode,
                Limits = new Response.RunLimits(problem.TimeoutSeconds, problem.MemoryMb),
                VisibleTests = [.. visible.Select(t => new Response.VisibleTest
                {
                    Name = t.Name,
                    Args = Json(t.ArgsJson),
                    Expected = t.ExpectedJson is null ? null : Json(t.ExpectedJson),
                    ExpectedException = t.ExpectedException,
                    Compare = t.Compare,
                    TimeBudgetMs = t.TimeBudgetMs,
                    AllocBudgetBytes = t.AllocBudgetBytes
                })],
                HiddenTestCount = hiddenCount,
                PlanRules = problem.PlanRulesJson is null ? null : Json(problem.PlanRulesJson),
                Metrics = problem.MetricsJson is null ? null : Json(problem.MetricsJson)
            };
        }

        private static JsonElement Json(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("problems/{slug}", async (string slug, IQueryHandler<Query, Response> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new Query(slug), cancellationToken)).Match(Results.Ok, CustomResults.Problem))
            .AllowAnonymous()
            .WithName(nameof(GetProblem))
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags(Tags.Problems);
        }
    }
}
