using Web.Api.Authentication;
using Web.Api.Authorization;
using Web.Api.Common;
using Web.Api.Database;
using Web.Api.Features.Users;
using Web.Api.UnitTests.Abstractions;

namespace Web.Api.UnitTests.Users;

public sealed class GetMeHandlerTests : BaseHandlerTest
{
    private static IUserContext SignedInAs(string externalId, Guid? userId)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.IsAuthenticated.Returns(true);
        userContext.ExternalId.Returns(externalId);
        userContext.TryGetUserId(out Arg.Any<Guid>()).Returns(call =>
        {
            call[0] = userId ?? Guid.Empty;
            return userId is not null;
        });

        return userContext;
    }

    [Fact]
    public async Task Handle_Should_ReturnAccount_When_UserIsProvisioned()
    {
        // Arrange
        using ApplicationDbContext db = CreateDbContext();
        var user = User.Create("user_abc", "ada@example.com", new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc));
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var handler = new GetMe.Handler(db, SignedInAs("user_abc", user.Id), new PermissionProvider());

        // Act
        Result<GetMe.Response> result = await handler.Handle(new GetMe.Query(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(user.Id);
        result.Value.Email.ShouldBe("ada@example.com");
        result.Value.Permissions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_Should_ReturnNotProvisioned_When_NoLocalUserWasResolved()
    {
        // Arrange
        using ApplicationDbContext db = CreateDbContext();
        var handler = new GetMe.Handler(db, SignedInAs("user_new", userId: null), new PermissionProvider());

        // Act
        Result<GetMe.Response> result = await handler.Handle(new GetMe.Query(), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotProvisioned);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_ResolvedUserNoLongerExists()
    {
        // Arrange
        using ApplicationDbContext db = CreateDbContext();
        var missingId = Guid.NewGuid();
        var handler = new GetMe.Handler(db, SignedInAs("user_gone", missingId), new PermissionProvider());

        // Act
        Result<GetMe.Response> result = await handler.Handle(new GetMe.Query(), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.NotFound");
    }
}
