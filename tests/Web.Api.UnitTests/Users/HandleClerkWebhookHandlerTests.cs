using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Api.Authentication;
using Web.Api.Authorization;
using Web.Api.Common;
using Web.Api.Database;
using Web.Api.Features.Users;
using Web.Api.UnitTests.Abstractions;
using static Web.Api.Features.Users.HandleClerkWebhook;

namespace Web.Api.UnitTests.Users;

public sealed class HandleClerkWebhookHandlerTests : BaseHandlerTest
{
    private static readonly DateTime Now = new(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);
    private const string BootstrapEmail = "boss@example.com";

    private static Handler CreateHandler(ApplicationDbContext db, HybridCache? cache = null)
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var provisioner = new UserProvisioner(
            db,
            clock,
            Microsoft.Extensions.Options.Options.Create(new AdminOptions { BootstrapEmails = [BootstrapEmail] }),
            NullLogger<UserProvisioner>.Instance);

        return new Handler(db, provisioner, cache ?? CreateCache(), clock);
    }

    [Fact]
    public async Task Handle_Should_CreateLearner_When_UserCreated()
    {
        using ApplicationDbContext db = CreateDbContext();

        Result result = await CreateHandler(db).Handle(new Command(UserCreated, "user_1", "ada@example.com", EmailVerified: true), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        User user = await db.Users.SingleAsync(u => u.ExternalId == "user_1");
        user.Email.ShouldBe("ada@example.com");
        user.Role.ShouldBe(Role.Learner);
    }

    [Theory]
    [InlineData(true, Role.Admin)]
    [InlineData(false, Role.Learner)]
    public async Task Handle_Should_GrantAdminOnlyIfVerified_When_CreatedWithBootstrapEmail(bool verified, Role expected)
    {
        using ApplicationDbContext db = CreateDbContext();

        await CreateHandler(db).Handle(new Command(UserCreated, "user_boss", BootstrapEmail, verified), CancellationToken.None);

        (await db.Users.SingleAsync(u => u.ExternalId == "user_boss")).Role.ShouldBe(expected);
    }

    [Fact]
    public async Task Handle_Should_SkipProvisioning_When_EventHasNoEmail()
    {
        using ApplicationDbContext db = CreateDbContext();

        Result result = await CreateHandler(db).Handle(new Command(UserCreated, "user_phone_only", Email: null, EmailVerified: false), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await db.Users.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_UpdateEmail_When_UserUpdated()
    {
        using ApplicationDbContext db = CreateDbContext();
        db.Users.Add(User.Create("user_1", "old@example.com", Role.Author, Now));
        await db.SaveChangesAsync();

        await CreateHandler(db).Handle(new Command(UserUpdated, "user_1", "new@example.com", true), CancellationToken.None);

        User user = await db.Users.SingleAsync(u => u.ExternalId == "user_1");
        user.Email.ShouldBe("new@example.com");
        user.Role.ShouldBe(Role.Author);    // the webhook never changes roles
    }

    [Fact]
    public async Task Handle_Should_SoftDeleteAndDropCaches_When_UserDeleted()
    {
        using ApplicationDbContext db = CreateDbContext();
        db.Users.Add(User.Create("user_1", "ada@example.com", Role.Admin, Now));
        await db.SaveChangesAsync();
        HybridCache cache = CreateCache();
        await cache.SetAsync(UserCacheKeys.ByExternalId("user_1"), (Guid?)Guid.NewGuid());
        await cache.SetAsync(PermissionCacheKeys.ForUser("user_1"), new[] { Permissions.AdminAccess });

        Result result = await CreateHandler(db, cache).Handle(new Command(UserDeleted, "user_1", null, false), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        User user = await db.Users.SingleAsync(u => u.ExternalId == "user_1");
        user.DeletedAt.ShouldBe(Now);
        user.IsDisabled.ShouldBeTrue();
        (await cache.GetOrCreateAsync(PermissionCacheKeys.ForUser("user_1"), _ => ValueTask.FromResult<string[]?>(null))).ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_IgnoreUpdate_When_UserWasDeleted()
    {
        using ApplicationDbContext db = CreateDbContext();
        var user = User.Create("user_1", "ada@example.com", Role.Learner, Now);
        user.MarkDeleted(Now);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        string anonymised = user.Email;

        await CreateHandler(db).Handle(new Command(UserUpdated, "user_1", "back@example.com", true), CancellationToken.None);

        (await db.Users.SingleAsync()).Email.ShouldBe(anonymised);
    }

    [Theory]
    [InlineData(UserDeleted)]
    [InlineData("session.created")]
    public async Task Handle_Should_Succeed_When_NothingToDo(string eventType)
    {
        using ApplicationDbContext db = CreateDbContext();

        Result result = await CreateHandler(db).Handle(new Command(eventType, "user_unknown", null, false), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await db.Users.AnyAsync()).ShouldBeFalse();
    }
}
