using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Web.Api.Common;
using Web.Api.Common.Endpoints;
using Web.Api.Common.Extensions;
using Web.Api.Common.Messaging;
using Web.Api.Database;

namespace Web.Api.Features.Problems;

/// <summary>
/// <c>GET /problems</c> — the public catalog: Published problems only, filtered by track / difficulty / search,
/// sorted by an allow-list (QY-03), paged (≤ 100), cached 10 min per normalised query under
/// <c>problems:list:{hash}</c> (QY-05). Pro problems are listed (<c>isFree: false</c>); opening one is gated
/// in <see cref="GetProblem"/>.
/// </summary>
public static class GetProblems
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    private const int MaxSearchLength = 100;

    /// <summary>Allowed <c>sort</c> values; anything else falls back to <see cref="Recommended"/>.</summary>
    public const string Recommended = "recommended";
    public const string ByTitle = "title";
    public const string ByDifficulty = "difficulty";
    public const string Newest = "newest";

    private static readonly HashSet<string> Sorts = new(StringComparer.OrdinalIgnoreCase) { Recommended, ByTitle, ByDifficulty, Newest };

    public sealed record Query(
        Track? Track = null,
        Difficulty? Difficulty = null,
        string? Q = null,
        string? Sort = null,
        int Page = 1,
        int PageSize = DefaultPageSize) : IQuery<PagedList<Response>>;

    public sealed record Response
    {
        public required Guid Id { get; init; }

        public required string Slug { get; init; }

        public required string Title { get; init; }

        public required Track Track { get; init; }

        public required Difficulty Difficulty { get; init; }

        public required bool IsFree { get; init; }

        public required IReadOnlyList<string> Tags { get; init; }
    }

    /// <summary>The query after clamping and normalising — also the cache identity.</summary>
    private sealed record Normalized(Track? Track, Difficulty? Difficulty, string? Search, string Sort, int Page, int PageSize)
    {
        public static Normalized From(Query query)
        {
            // Slugs and tags are lowercase-canonical (NM-09), so lowercase is the right normal form here.
#pragma warning disable CA1308
            string? search = query.Q?.Trim().ToLowerInvariant();
#pragma warning restore CA1308
            if (search is { Length: > MaxSearchLength })
            {
                search = search[..MaxSearchLength];
            }

            string sort = query.Sort is not null && Sorts.TryGetValue(query.Sort.Trim(), out string? known) ? known : Recommended;

            return new Normalized(
                query.Track,
                query.Difficulty,
                string.IsNullOrEmpty(search) ? null : search,
                sort,
                Math.Max(1, query.Page),
                Math.Clamp(query.PageSize, 1, MaxPageSize));
        }

        public string CacheKey => ProblemCacheKeys.List($"{Track}|{Difficulty}|{Search}|{Sort}|{Page}|{PageSize}");
    }

    internal sealed class Handler(ApplicationDbContext db, HybridCache cache) : IQueryHandler<Query, PagedList<Response>>
    {
        public async Task<Result<PagedList<Response>>> Handle(Query query, CancellationToken cancellationToken)
        {
            var normalized = Normalized.From(query);

            return await cache.GetOrCreateAsync(
                normalized.CacheKey,
                (db, normalized),
                static async (state, ct) => await LoadAsync(state.db, state.normalized, ct),
                ProblemCacheKeys.EntryOptions,
                ProblemCacheKeys.Tags,
                cancellationToken);
        }

        private static Task<PagedList<Response>> LoadAsync(ApplicationDbContext db, Normalized query, CancellationToken cancellationToken)
        {
            IQueryable<Problem> problems = db.Problems.AsNoTracking().Where(p => p.Status == ProblemStatus.Published);

            if (query.Track is { } track)
            {
                problems = problems.Where(p => p.Track == track);
            }

            if (query.Difficulty is { } difficulty)
            {
                problems = problems.Where(p => p.Difficulty == difficulty);
            }

            if (query.Search is { } search)
            {
                // Translated to SQL (lower(title), strpos): parameterised, so no LIKE wildcards to escape, and the
                // .NET culture/comparison overloads these analyzers ask for don't exist in the database.
#pragma warning disable CA1304, CA1311, CA1862
                problems = problems.Where(p =>
                    p.Title.ToLower().Contains(search) || p.Slug.Contains(search) || p.Tags.Contains(search));
#pragma warning restore CA1304, CA1311, CA1862
            }

            problems = query.Sort switch
            {
                ByTitle => problems.OrderBy(p => p.Title).ThenBy(p => p.Slug),
                // Stored as text, so rank explicitly: Easy, then Medium, then Hard.
                ByDifficulty => problems
                    .OrderByDescending(p => p.Difficulty == Difficulty.Easy)
                    .ThenByDescending(p => p.Difficulty == Difficulty.Medium)
                    .ThenBy(p => p.SortOrder).ThenBy(p => p.Slug),
                Newest => problems.OrderByDescending(p => p.PublishedAt).ThenBy(p => p.Slug),
                _ => problems.OrderBy(p => p.SortOrder).ThenBy(p => p.Title).ThenBy(p => p.Slug)
            };

            IQueryable<Response> responses = problems.Select(p => new Response
            {
                Id = p.Id,
                Slug = p.Slug,
                Title = p.Title,
                Track = p.Track,
                Difficulty = p.Difficulty,
                IsFree = p.IsFree,
                Tags = p.Tags
            });

            return PagedList<Response>.CreateAsync(responses, query.Page, query.PageSize, cancellationToken);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            // Enums bind as strings: the built-in enum binding is case-sensitive, so it would reject the very
            // camelCase values this API returns ("algorithms", "easy").
            app.MapGet("problems", async (
                string? track,
                string? difficulty,
                string? q,
                string? sort,
                int? page,
                int? pageSize,
                IQueryHandler<Query, PagedList<Response>> handler,
                CancellationToken cancellationToken) =>
            {
                if (!TryParseFilter(track, out Track? trackFilter) || !TryParseFilter(difficulty, out Difficulty? difficultyFilter))
                {
                    return CustomResults.Problem(ProblemErrors.InvalidFilter);
                }

                var query = new Query(trackFilter, difficultyFilter, q, sort, page ?? 1, pageSize ?? DefaultPageSize);

                return (await handler.Handle(query, cancellationToken)).Match(Results.Ok, CustomResults.Problem);
            })
            .AllowAnonymous()
            .WithName(nameof(GetProblems))
            .Produces<PagedList<Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags(Tags.Problems);
        }

        /// <summary>Absent → no filter; a named member (any case) → that value; anything else (incl. numbers) → invalid.</summary>
        private static bool TryParseFilter<TEnum>(string? value, out TEnum? result)
            where TEnum : struct, Enum
        {
            result = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (value.Any(char.IsAsciiDigit) || !Enum.TryParse(value.Trim(), ignoreCase: true, out TEnum parsed) || !Enum.IsDefined(parsed))
            {
                return false;
            }

            result = parsed;
            return true;
        }
    }
}
