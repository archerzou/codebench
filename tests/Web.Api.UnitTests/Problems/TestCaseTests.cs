using Web.Api.Features.Problems;

namespace Web.Api.UnitTests.Problems;

public sealed class TestCaseTests
{
    private static readonly Guid ProblemId = Guid.NewGuid();

    private static TestCaseContent Visible(string? expectedJson = "6", string? expectedException = null) => new()
    {
        Name = "Example",
        ArgsJson = "[[1, 2, 3, 4]]",
        ExpectedJson = expectedJson,
        ExpectedException = expectedException
    };

    [Fact]
    public void Create_Should_StoreDataDrivenTest_When_ContentIsValid()
    {
        var testCase = TestCase.Create(ProblemId, 0, Visible() with { IsHidden = true, TimeBudgetMs = 30, AllocBudgetBytes = 1024 });

        testCase.ProblemId.ShouldBe(ProblemId);
        testCase.Order.ShouldBe(0);
        testCase.IsHidden.ShouldBeTrue();
        testCase.ArgsJson.ShouldBe("[[1, 2, 3, 4]]");
        testCase.ExpectedJson.ShouldBe("6");
        testCase.ExpectedException.ShouldBeNull();
        testCase.Compare.ShouldBe(CompareMode.Exact);
        testCase.TimeBudgetMs.ShouldBe(30);
        testCase.AllocBudgetBytes.ShouldBe(1024);
    }

    [Fact]
    public void Create_Should_AcceptExpectedException_When_NoExpectedValue()
    {
        var testCase = TestCase.Create(ProblemId, 1, Visible(expectedJson: "", expectedException: "ArgumentException"));

        testCase.ExpectedJson.ShouldBeNull();
        testCase.ExpectedException.ShouldBe("ArgumentException");
    }

    [Theory]
    [InlineData("6", "ArgumentException")]   // both
    [InlineData(null, null)]                 // neither
    [InlineData("  ", "")]                   // blanks count as missing
    public void Create_Should_Throw_When_NotExactlyOneExpectation(string? expectedJson, string? expectedException)
    {
        Should.Throw<ArgumentException>(() => TestCase.Create(ProblemId, 0, Visible(expectedJson, expectedException)));
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(-5, null)]
    [InlineData(null, -1L)]
    public void Create_Should_Throw_When_BudgetIsOutOfRange(int? timeBudgetMs, long? allocBudgetBytes)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            TestCase.Create(ProblemId, 0, Visible() with { TimeBudgetMs = timeBudgetMs, AllocBudgetBytes = allocBudgetBytes }));
    }

    [Fact]
    public void Create_Should_Throw_When_OrderIsNegative()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => TestCase.Create(ProblemId, -1, Visible()));
    }

    [Fact]
    public void MoveTo_Should_ChangeOrder_When_NonNegative()
    {
        var testCase = TestCase.Create(ProblemId, 0, Visible());

        testCase.MoveTo(3);

        testCase.Order.ShouldBe(3);
        Should.Throw<ArgumentOutOfRangeException>(() => testCase.MoveTo(-1));
    }
}
