using FluentValidation.TestHelper;
using static Web.Api.Features.Users.HandleClerkWebhook;

namespace Web.Api.UnitTests.Users;

public sealed class HandleClerkWebhookValidatorTests
{
    private readonly Validator _validator = new();

    [Fact]
    public void Validate_Should_Pass_When_CommandIsComplete()
    {
        _validator.TestValidate(new Command(UserCreated, "user_1", "ada@example.com", true)).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_Should_Pass_When_EmailIsAbsent()
    {
        _validator.TestValidate(new Command(UserDeleted, "user_1", null, false)).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Should_Fail_When_ExternalIdIsBlank(string externalId)
    {
        _validator.TestValidate(new Command(UserCreated, externalId, "ada@example.com", true)).ShouldHaveValidationErrorFor(c => c.ExternalId);
    }

    [Fact]
    public void Validate_Should_Fail_When_EventTypeIsBlank()
    {
        _validator.TestValidate(new Command("", "user_1", null, false)).ShouldHaveValidationErrorFor(c => c.EventType);
    }

    [Fact]
    public void Validate_Should_Fail_When_EmailIsMalformed()
    {
        _validator.TestValidate(new Command(UserCreated, "user_1", "not-an-email", true)).ShouldHaveValidationErrorFor(c => c.Email);
    }
}
