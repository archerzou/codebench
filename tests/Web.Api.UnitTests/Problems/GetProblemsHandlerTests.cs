using Microsoft.Extensions.Caching.Hybrid;
using Web.Api.Common;
using Web.Api.Database;
using Web.Api.Features.Problems;
using Web.Api.UnitTests.Abstractions;
using static Web.Api.UnitTests.Problems.ProblemTestData;

namespace Web.Api.UnitTests.Problems;

public sealed class GetProblemsHandlerTests : BaseHandlerTest
{
    private static async Task<PagedList<GetProblems.Response>> HandleAsync(ApplicationDbContext db, GetProblems.Query query, HybridCache? cache = null)
    {
        Result<PagedList<GetProblems.Response>> result = await new GetProblems.Handler(db, cache ?? CreateCache()).Handle(query, CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    [Fact]
    public async Task Handle_Should_ListOnlyPublishedProblems_When_StatusesAreMixed()
    {
        using ApplicationDbContext db = CreateDbContext();
        await AddAsync(db, Content("live"));
        await AddAsync(db, Content("draft"), ProblemStatus.Draft);
        await AddAsync(db, Content("retired"), ProblemStatus.Archived);

        PagedList<GetProblems.Response> page = await HandleAsync(db, new GetProblems.Query());

        page.Items.Select(p => p.Slug).ShouldBe(["live"]);
        page.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_Filter_When_TrackAndDifficultyGiven()
    {
        using ApplicationDbContext db = CreateDbContext();
        await AddAsync(db, Content("a-easy", Track.Algorithms, Difficulty.Easy));
        await AddAsync(db, Content("a-medium", Track.Algorithms, Difficulty.Medium));
        await AddAsync(db, Content("d-easy", Track.Database, Difficulty.Easy));

        PagedList<GetProblems.Response> page = await HandleAsync(db, new GetProblems.Query(Track.Algorithms, Difficulty.Easy));

        page.Items.Select(p => p.Slug).ShouldBe(["a-easy"]);
    }

    [Theory]
    [InlineData("MERGE")]       // title, case-insensitive
    [InlineData("intervals")]   // slug fragment
    [InlineData("sorting")]     // exact tag
    [InlineData("  merge  ")]   // trimmed
    public async Task Handle_Should_Search_When_QueryMatchesTitleSlugOrTag(string q)
    {
        using ApplicationDbContext db = CreateDbContext();
        await AddAsync(db, Content("merge-intervals", title: "Merge Booking Windows", tags: ["sorting"]));
        await AddAsync(db, Content("sum-of-evens", title: "Sum of Evens", tags: ["arrays"]));

        PagedList<GetProblems.Response> page = await HandleAsync(db, new GetProblems.Query(Q: q));

        page.Items.Select(p => p.Slug).ShouldBe(["merge-intervals"]);
    }

    [Theory]
    [InlineData(null, new[] { "first", "second", "third" })]
    [InlineData("recommended", new[] { "first", "second", "third" })]
    [InlineData("TITLE", new[] { "third", "second", "first" })]
    [InlineData("difficulty", new[] { "second", "third", "first" })]
    [InlineData("drop table problems", new[] { "first", "second", "third" })]   // unknown → recommended
    public async Task Handle_Should_SortByAllowList_When_SortGiven(string? sort, string[] expected)
    {
        using ApplicationDbContext db = CreateDbContext();
        await AddAsync(db, Content("first", difficulty: Difficulty.Hard, sortOrder: 10, title: "Zebra"));
        await AddAsync(db, Content("second", difficulty: Difficulty.Easy, sortOrder: 20, title: "Yak"));
        await AddAsync(db, Content("third", difficulty: Difficulty.Medium, sortOrder: 30, title: "Ant"));

        PagedList<GetProblems.Response> page = await HandleAsync(db, new GetProblems.Query(Sort: sort));

        page.Items.Select(p => p.Slug).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 2, 1, 2)]
    [InlineData(-5, 2, 1, 2)]
    [InlineData(2, 2, 2, 2)]
    [InlineData(1, 0, 1, 1)]
    [InlineData(1, 500, 1, GetProblems.MaxPageSize)]
    public async Task Handle_Should_ClampPaging_When_OutOfRange(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        using ApplicationDbContext db = CreateDbContext();
        for (int i = 0; i < 5; i++)
        {
            await AddAsync(db, Content($"p{i}", sortOrder: i));
        }

        PagedList<GetProblems.Response> result = await HandleAsync(db, new GetProblems.Query(Page: page, PageSize: pageSize));

        result.Page.ShouldBe(expectedPage);
        result.PageSize.ShouldBe(expectedPageSize);
        result.TotalCount.ShouldBe(5);
        result.Items.Count.ShouldBe(Math.Min(expectedPageSize, 5 - (expectedPage - 1) * expectedPageSize));
    }

    [Fact]
    public async Task Handle_Should_ServeCachedPageUntilTagCleared_When_CatalogChanges()
    {
        using ApplicationDbContext db = CreateDbContext();
        HybridCache cache = CreateCache();
        await AddAsync(db, Content("one"));
        (await HandleAsync(db, new GetProblems.Query(), cache)).TotalCount.ShouldBe(1);

        await AddAsync(db, Content("two"));
        (await HandleAsync(db, new GetProblems.Query(), cache)).TotalCount.ShouldBe(1);   // cached (QY-05)

        await cache.RemoveByTagAsync(ProblemCacheKeys.Tag);
        (await HandleAsync(db, new GetProblems.Query(), cache)).TotalCount.ShouldBe(2);
    }

    [Fact]
    public void List_Should_GiveEqualKeys_When_QueriesNormaliseTheSame()
    {
        ProblemCacheKeys.List("Algorithms|Easy|merge|title|1|20").ShouldBe(ProblemCacheKeys.List("Algorithms|Easy|merge|title|1|20"));
        ProblemCacheKeys.List("a").ShouldNotBe(ProblemCacheKeys.List("b"));
        ProblemCacheKeys.List("a").ShouldMatch("^problems:list:[0-9a-f]{16}$");
    }
}
