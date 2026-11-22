using Web.Api.Common;
using Web.Api.Features.Problems;

namespace Web.Api.UnitTests.Problems;

public sealed class ProblemTests
{
    private static readonly DateTime CreatedAt = new(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PublishedAt = CreatedAt.AddHours(1);

    private static ProblemContent Content(string slug = "sum-of-evens") => new()
    {
        Slug = slug,
        Title = "Sum of Evens",
        Track = Track.Algorithms,
        Difficulty = Difficulty.Easy,
        GraderKind = GraderKind.Algorithms,
        IsFree = true,
        Signature = "public long SumOfEvens(int[] nums)",
        StatementMarkdown = "Return the sum of all even numbers.",
        StarterCode = "public class Solution { }",
        ReferenceSolution = "public class Solution { /* reference */ }",
        Tags = ["arrays", "allocations"]
    };

    [Fact]
    public void CreateDraft_Should_StartAsUnpublishedDraft_When_Created()
    {
        var problem = Problem.CreateDraft(Content(), CreatedAt);

        problem.Status.ShouldBe(ProblemStatus.Draft);
        problem.Version.ShouldBe(0);
        problem.PublishedAt.ShouldBeNull();
        problem.Limits.ShouldBe(ProblemLimits.Default);
        problem.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ProblemCreatedDomainEvent>();
    }

    [Fact]
    public void CreateDraft_Should_NotShareLimitsInstances_When_ContentIsReused()
    {
        ProblemContent content = Content();

        var first = Problem.CreateDraft(content, CreatedAt);
        var second = Problem.CreateDraft(content with { Slug = "other" }, CreatedAt);

        first.Limits.ShouldBe(second.Limits);                                   // same values…
        ReferenceEquals(first.Limits, second.Limits).ShouldBeFalse();           // …never the same owned instance
        ReferenceEquals(ProblemLimits.Default, ProblemLimits.Default).ShouldBeFalse();
    }

    [Fact]
    public void CreateDraft_Should_CopyTags_When_ContentIsReused()
    {
        var tags = new List<string> { "arrays" };
        var problem = Problem.CreateDraft(Content() with { Tags = tags }, CreatedAt);

        tags.Add("mutated-later");

        problem.Tags.ShouldBe(["arrays"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateDraft_Should_Throw_When_SlugIsBlank(string slug)
    {
        Should.Throw<ArgumentException>(() => Problem.CreateDraft(Content(slug), CreatedAt));
    }

    [Fact]
    public void Publish_Should_GoLiveAsVersionOne_When_Draft()
    {
        var problem = Problem.CreateDraft(Content(), CreatedAt);
        problem.ClearDomainEvents();

        Result result = problem.Publish(PublishedAt);

        result.IsSuccess.ShouldBeTrue();
        problem.Status.ShouldBe(ProblemStatus.Published);
        problem.Version.ShouldBe(1);
        problem.PublishedAt.ShouldBe(PublishedAt);
        problem.DomainEvents.ShouldHaveSingleItem().ShouldBe(new ProblemPublishedDomainEvent(problem.Id, "sum-of-evens", 1));
    }

    [Fact]
    public void Publish_Should_BeNoOp_When_AlreadyPublished()
    {
        var problem = Problem.CreateDraft(Content(), CreatedAt);
        problem.Publish(PublishedAt);
        problem.ClearDomainEvents();

        Result result = problem.Publish(PublishedAt.AddDays(1));

        result.IsSuccess.ShouldBeTrue();
        problem.Version.ShouldBe(1);
        problem.PublishedAt.ShouldBe(PublishedAt);
        problem.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Publish_Should_ReturnConflict_When_Archived()
    {
        var problem = Problem.CreateDraft(Content(), CreatedAt);
        problem.Publish(PublishedAt);
        problem.Archive();

        Result result = problem.Publish(PublishedAt.AddDays(1));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Problems.CannotPublishArchived");
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        problem.Status.ShouldBe(ProblemStatus.Archived);
        problem.Version.ShouldBe(1);
    }

    [Fact]
    public void Archive_Should_ArchiveAndKeepVersion_When_Published()
    {
        var problem = Problem.CreateDraft(Content(), CreatedAt);
        problem.Publish(PublishedAt);
        problem.ClearDomainEvents();

        Result result = problem.Archive();

        result.IsSuccess.ShouldBeTrue();
        problem.Status.ShouldBe(ProblemStatus.Archived);
        problem.Version.ShouldBe(1);
        problem.DomainEvents.ShouldHaveSingleItem().ShouldBe(new ProblemArchivedDomainEvent(problem.Id, "sum-of-evens", 1));
    }

    [Fact]
    public void Archive_Should_BeNoOp_When_AlreadyArchived()
    {
        var problem = Problem.CreateDraft(Content(), CreatedAt);
        problem.Publish(PublishedAt);
        problem.Archive();
        problem.ClearDomainEvents();

        Result result = problem.Archive();

        result.IsSuccess.ShouldBeTrue();
        problem.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Archive_Should_ReturnConflict_When_Draft()
    {
        var problem = Problem.CreateDraft(Content(), CreatedAt);

        Result result = problem.Archive();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Problems.CannotArchiveDraft");
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        problem.Status.ShouldBe(ProblemStatus.Draft);
    }
}
