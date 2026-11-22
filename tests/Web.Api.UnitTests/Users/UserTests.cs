using Web.Api.Common;
using Web.Api.Features.Users;

namespace Web.Api.UnitTests.Users;

public sealed class UserTests
{
    private static readonly DateTime CreatedAt = new(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_Should_StartFreeEnabledAndReal_When_Created()
    {
        var user = User.Create("user_abc", "ada@example.com", Role.Learner, CreatedAt);

        user.Role.ShouldBe(Role.Learner);
        user.Plan.ShouldBe(Plan.Free);
        user.IsDisabled.ShouldBeFalse();
        user.IsDemo.ShouldBeFalse();
        user.DomainEvents.ShouldHaveSingleItem().ShouldBe(new UserCreatedDomainEvent(user.Id, Role.Learner));
    }

    [Fact]
    public void CreateDemo_Should_MarkDemoWithSeedExternalId_When_Created()
    {
        var user = User.CreateDemo("demo-ana", "demo-ana@codebench.test", Role.Learner, CreatedAt);

        user.IsDemo.ShouldBeTrue();
        user.ExternalId.ShouldBe("seed|demo-ana");
        user.ExternalId.ShouldStartWith(User.DemoExternalIdPrefix);
    }

    [Fact]
    public void ChangeRole_Should_RaiseEventOnce_When_RoleChanges()
    {
        var user = User.Create("user_abc", "ada@example.com", Role.Learner, CreatedAt);
        user.ClearDomainEvents();

        user.ChangeRole(Role.Author);
        user.ChangeRole(Role.Author);

        user.Role.ShouldBe(Role.Author);
        user.DomainEvents.ShouldHaveSingleItem().ShouldBe(new UserRoleChangedDomainEvent(user.Id, "user_abc", Role.Author));
    }

    [Fact]
    public void Disable_Should_BlockUntilEnabled_When_Called()
    {
        var user = User.Create("user_abc", "ada@example.com", Role.Learner, CreatedAt);

        user.Disable();
        user.IsDisabled.ShouldBeTrue();

        user.Enable().IsSuccess.ShouldBeTrue();
        user.IsDisabled.ShouldBeFalse();
    }

    [Theory]
    [InlineData("", "ada@example.com")]
    [InlineData("user_abc", " ")]
    public void Create_Should_Throw_When_IdentityIsBlank(string externalId, string email)
    {
        Should.Throw<ArgumentException>(() => User.Create(externalId, email, Role.Learner, CreatedAt));
    }

    [Fact]
    public void MarkDeleted_Should_DisableAndAnonymise_When_ClerkAccountIsDeleted()
    {
        var user = User.Create("user_abc", "ada@example.com", Role.Admin, CreatedAt);
        user.ClearDomainEvents();

        user.MarkDeleted(CreatedAt.AddDays(1));
        user.MarkDeleted(CreatedAt.AddDays(2));     // idempotent

        user.DeletedAt.ShouldBe(CreatedAt.AddDays(1));
        user.IsDisabled.ShouldBeTrue();
        user.Email.ShouldNotContain("ada");
        user.ExternalId.ShouldBe("user_abc");
        user.DomainEvents.ShouldHaveSingleItem().ShouldBe(new UserDeletedDomainEvent(user.Id, "user_abc"));
    }

    [Fact]
    public void Enable_Should_ReturnConflict_When_UserWasDeleted()
    {
        var user = User.Create("user_abc", "ada@example.com", Role.Learner, CreatedAt);
        user.MarkDeleted(CreatedAt);

        Result result = user.Enable();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.Deleted");
        user.IsDisabled.ShouldBeTrue();
    }
}