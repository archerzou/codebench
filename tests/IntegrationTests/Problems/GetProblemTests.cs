using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Web.Api.Features.Problems;
using Web.Api.Features.Users;

namespace IntegrationTests.Problems;

/// <summary>1.6.2 / 1.11.3: problem detail over real HTTP — hidden data never in the JSON (QY-04, TS-03).</summary>
public sealed class GetProblemTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task GetProblem_Should_ReturnEditorData_When_FreeProblemIsRequestedAnonymously()
    {
        await SeedProblemsAsync();

        GetProblem.Response? problem = await HttpClient.GetFromJsonAsync<GetProblem.Response>("problems/sum-of-evens", TestJson.Options);

        problem.ShouldNotBeNull();
        problem.Title.ShouldBe("Sum of Evens");
        problem.Signature.ShouldBe("public long SumOfEvens(int[] nums)");
        problem.StarterCode.ShouldContain("throw new NotImplementedException()");
        problem.Limits.ShouldBe(new GetProblem.Response.RunLimits(10, 256));
        problem.VisibleTests.Select(t => t.Name).ShouldBe(["Example", "Empty", "Mixed signs"]);
        JsonNode.DeepEquals(JsonNode.Parse(problem.VisibleTests[0].Args.GetRawText()), JsonNode.Parse("[[1,2,3,4]]")).ShouldBeTrue();
        problem.HiddenTestCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetProblem_Should_NeverContainHiddenTestsOrReference_When_SerialisedToJson()
    {
        await SeedProblemsAsync();

        string json = await HttpClient.GetStringAsync("problems/sum-of-evens");

        // Hidden test names, hidden generator args, and the reference solution's body / field name.
        json.ShouldNotContain("All odd");
        json.ShouldNotContain("200k elements");
        json.ShouldNotContain("$gen");
        json.ShouldNotContain("randomInts");
        json.ShouldNotContain("allocBudgetBytes\":1024");
        json.ShouldNotContain("sum += n");
        json.ShouldNotContain("reference", Case.Insensitive);
        json.ShouldContain("\"hiddenTestCount\":2");
    }

    [Fact]
    public async Task GetProblem_Should_Return404_When_SlugIsUnknown()
    {
        HttpResponseMessage response = await HttpClient.GetAsync("problems/does-not-exist");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Problems.NotFound");
    }

    [Fact]
    public async Task GetProblem_Should_Return404_When_ProProblemIsRequestedAnonymously()
    {
        await SeedProblemsAsync();

        (await HttpClient.GetAsync("problems/top-k-words")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(Role.Learner, Plan.Free, HttpStatusCode.NotFound)]
    [InlineData(Role.Learner, Plan.Pro, HttpStatusCode.OK)]
    [InlineData(Role.Author, Plan.Free, HttpStatusCode.OK)]
    [InlineData(Role.Admin, Plan.Free, HttpStatusCode.OK)]
    public async Task GetProblem_Should_GateProProblem_When_SignedIn(Role role, Plan plan, HttpStatusCode expected)
    {
        await SeedProblemsAsync();
        await SignInAsync(role, plan);

        (await HttpClient.GetAsync("problems/top-k-words")).StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task GetProblem_Should_ServeFreeProblemFromCache_When_RequestedByDifferentCallers()
    {
        await SeedProblemsAsync();
        (await HttpClient.GetAsync("problems/merge-intervals")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The cached detail is caller-independent; the Pro check runs per request on top of it.
        await SignInAsync(Role.Learner);
        GetProblem.Response? again = await HttpClient.GetFromJsonAsync<GetProblem.Response>("problems/merge-intervals", TestJson.Options);

        again.ShouldNotBeNull().VisibleTests.ShouldAllBe(t => t.Compare == CompareMode.UnorderedNested);
    }
}
