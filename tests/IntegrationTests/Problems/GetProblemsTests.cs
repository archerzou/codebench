using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Web.Api.Common;
using Web.Api.Features.Problems;

namespace IntegrationTests.Problems;

/// <summary>1.6.1 / 1.11.4: the public catalog over real HTTP + Postgres.</summary>
public sealed class GetProblemsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private Task<PagedList<GetProblems.Response>?> GetAsync(string url) =>
        HttpClient.GetFromJsonAsync<PagedList<GetProblems.Response>>(url, TestJson.Options);

    [Fact]
    public async Task GetProblems_Should_ListSeededProblemsInOrder_When_Anonymous()
    {
        await SeedProblemsAsync();

        PagedList<GetProblems.Response>? page = await GetAsync("problems?track=algorithms&pageSize=100");

        page.ShouldNotBeNull();
        string[] seeded = ["sum-of-evens", "first-unique-char", "merge-intervals", "top-k-words"];
        page.Items.Select(p => p.Slug).Where(seeded.Contains).ShouldBe(seeded);   // sortOrder 10…40
        page.Items.Single(p => p.Slug == "top-k-words").IsFree.ShouldBeFalse();   // Pro problems are listed
    }

    [Fact]
    public async Task GetProblems_Should_SerialiseEnumsAsCamelCaseStrings_When_Returned()
    {
        await SeedProblemsAsync();

        var json = JsonNode.Parse(await HttpClient.GetStringAsync("problems?q=sum-of-evens"));

        JsonNode item = json!["items"]!.AsArray().Single(i => i!["slug"]!.GetValue<string>() == "sum-of-evens")!;
        item["track"]!.GetValue<string>().ShouldBe("algorithms");
        item["difficulty"]!.GetValue<string>().ShouldBe("easy");
        json["page"]!.GetValue<int>().ShouldBe(1);
        json["hasNextPage"].ShouldNotBeNull();
    }

    [Fact]
    public async Task GetProblems_Should_Filter_When_DifficultyAndSearchGiven()
    {
        await SeedProblemsAsync();

        PagedList<GetProblems.Response>? page = await GetAsync("problems?difficulty=medium&q=booking");

        page.ShouldNotBeNull().Items.Select(p => p.Slug).ShouldBe(["merge-intervals"]);
    }

    [Fact]
    public async Task GetProblems_Should_ClampPageSize_When_TooLarge()
    {
        PagedList<GetProblems.Response>? page = await GetAsync("problems?pageSize=5000");

        page.ShouldNotBeNull().PageSize.ShouldBe(GetProblems.MaxPageSize);
    }

    [Theory]
    [InlineData("problems?track=quantum")]
    [InlineData("problems?difficulty=1")]          // numbers aren't names
    [InlineData("problems?page=abc")]              // unbindable → 400 via BadHttpRequestException, not 500
    public async Task GetProblems_Should_Return400_When_QueryIsInvalid(string requestPath)
    {
        HttpResponseMessage response = await HttpClient.GetAsync(requestPath);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Theory]
    [InlineData("problems?track=algorithms&difficulty=easy")]
    [InlineData("problems?track=Algorithms&difficulty=EASY")]
    public async Task GetProblems_Should_AcceptEnumFiltersInAnyCase_When_Named(string requestPath)
    {
        await SeedProblemsAsync();

        PagedList<GetProblems.Response>? page = await GetAsync(requestPath);

        page.ShouldNotBeNull().Items.ShouldContain(p => p.Slug == "sum-of-evens");
    }

    [Fact]
    public async Task GetProblems_Should_TreatSearchAsText_When_ItContainsWildcards()
    {
        await SeedProblemsAsync();

        PagedList<GetProblems.Response>? page = await GetAsync("problems?q=%25");   // "%"

        page.ShouldNotBeNull().Items.ShouldBeEmpty();
    }
}
