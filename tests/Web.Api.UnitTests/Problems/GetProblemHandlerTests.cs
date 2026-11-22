using System.Reflection;
using Web.Api.Authentication;
using Web.Api.Authorization;
using Web.Api.Common;
using Web.Api.Database;
using Web.Api.Features.Problems;
using Web.Api.Features.Users;
using Web.Api.UnitTests.Abstractions;
using static Web.Api.UnitTests.Problems.ProblemTestData;

namespace Web.Api.UnitTests.Problems;

public sealed class GetProblemHandlerTests : BaseHandlerTest
{
    private static IUserContext Anonymous()
    {
        IUserContext context = Substitute.For<IUserContext>();
        context.IsAuthenticated.Returns(false);
        context.TryGetUserId(out Arg.Any<Guid>()).Returns(false);
        return context;
    }

    private static async Task<IUserContext> SignedInAsync(ApplicationDbContext db, Role role, Plan plan = Plan.Free, bool disabled = false)
    {
        if (!db.RolePermissions.Any())
        {
            db.RolePermissions.AddRange(RolePermissionMatrix.Rows.Select(r => RolePermission.Create(r.Role, r.Permission)));
        }

        var user = User.Create($"user_{Guid.NewGuid():N}", "someone@example.com", role, Now);
        user.ChangePlan(plan);
        if (disabled)
        {
            user.Disable();
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();

        IUserContext context = Substitute.For<IUserContext>();
        context.IsAuthenticated.Returns(true);
        context.ExternalId.Returns(user.ExternalId);
        context.TryGetUserId(out Arg.Any<Guid>()).Returns(call => { call[0] = user.Id; return true; });
        return context;
    }

    private static Task<Result<GetProblem.Response>> HandleAsync(ApplicationDbContext db, IUserContext user, string slug) =>
        new GetProblem.Handler(db, CreateCache(), user, new PermissionProvider(db, CreateCache()))
            .Handle(new GetProblem.Query(slug), CancellationToken.None);

    [Fact]
    public async Task Handle_Should_ReturnVisibleTestsAndHiddenCount_When_ProblemIsPublished()
    {
        using ApplicationDbContext db = CreateDbContext();
        await AddAsync(db, Content("sum-of-evens"), ProblemStatus.Published,
            Test("Example", args: "[[1,2,3,4]]", expected: "6"),
            Test("Secret one", hidden: true, args: """[{"$gen":"randomInts"}]"""),
            Test("Empty", args: "[[]]", expected: "0"),
            Test("Secret two", hidden: true));

        Result<GetProblem.Response> result = await HandleAsync(db, Anonymous(), "sum-of-evens");

        result.IsSuccess.ShouldBeTrue();
        GetProblem.Response problem = result.Value;
        problem.VisibleTests.Select(t => t.Name).ShouldBe(["Example", "Empty"]);
        problem.VisibleTests[0].Args.GetRawText().ShouldBe("[[1,2,3,4]]");
        problem.VisibleTests[0].Expected!.Value.GetRawText().ShouldBe("6");
        problem.HiddenTestCount.ShouldBe(2);
        problem.Version.ShouldBe(1);
        problem.StarterCode.ShouldBe("public class Solution { }");
    }

    [Fact]
    public void Response_Should_HaveNoFieldForReferenceOrHiddenData_When_Inspected()
    {
        // QY-04 as a property of the type: nothing here can carry the reference or hidden tests.
        string[] names = [.. new[] { typeof(GetProblem.Response), typeof(GetProblem.Response.VisibleTest) }
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Select(p => p.Name)];

        names.ShouldNotContain(n => n.Contains("Reference", StringComparison.OrdinalIgnoreCase));
        names.Where(n => n.Contains("Hidden", StringComparison.OrdinalIgnoreCase)).ShouldBe(["HiddenTestCount"]);
    }

    [Theory]
    [InlineData(ProblemStatus.Draft)]
    [InlineData(ProblemStatus.Archived)]
    public async Task Handle_Should_ReturnNotFound_When_ProblemIsNotPublished(ProblemStatus status)
    {
        using ApplicationDbContext db = CreateDbContext();
        await AddAsync(db, Content("not-live"), status, Test("Example"));

        Result<GetProblem.Response> result = await HandleAsync(db, Anonymous(), "not-live");

        result.Error.Code.ShouldBe("Problems.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_SlugIsUnknown()
    {
        using ApplicationDbContext db = CreateDbContext();

        (await HandleAsync(db, Anonymous(), "nope")).Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_Should_HideProProblem_When_Anonymous()
    {
        using ApplicationDbContext db = CreateDbContext();
        await AddAsync(db, Content("top-k-words", isFree: false), ProblemStatus.Published, Test("Example"));

        (await HandleAsync(db, Anonymous(), "top-k-words")).Error.Code.ShouldBe("Problems.NotFound");
    }

    [Theory]
    [InlineData(Role.Learner, Plan.Free, false, false)]
    [InlineData(Role.Learner, Plan.Pro, false, true)]
    [InlineData(Role.Learner, Plan.Pro, true, false)]    // disabled Pro user
    [InlineData(Role.Author, Plan.Free, false, true)]    // staff preview
    [InlineData(Role.Admin, Plan.Free, false, true)]
    public async Task Handle_Should_GateProProblem_When_SignedIn(Role role, Plan plan, bool disabled, bool visible)
    {
        using ApplicationDbContext db = CreateDbContext();
        await AddAsync(db, Content("top-k-words", isFree: false), ProblemStatus.Published, Test("Example"));
        IUserContext user = await SignedInAsync(db, role, plan, disabled);

        Result<GetProblem.Response> result = await HandleAsync(db, user, "top-k-words");

        result.IsSuccess.ShouldBe(visible);
    }
}
