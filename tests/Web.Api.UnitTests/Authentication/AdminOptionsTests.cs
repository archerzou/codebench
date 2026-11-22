using Web.Api.Authentication;

namespace Web.Api.UnitTests.Authentication;

public sealed class AdminOptionsTests
{
    private static readonly AdminOptions Options = new() { BootstrapEmails = ["Admin@Example.com", "", "  ", " ops@example.com "] };

    [Theory]
    [InlineData("admin@example.com")]
    [InlineData("ADMIN@EXAMPLE.COM")]
    [InlineData(" admin@example.com ")]
    [InlineData("ops@example.com")]
    public void IsBootstrapEmail_Should_ReturnTrue_When_EmailIsListed(string email)
    {
        Options.IsBootstrapEmail(email).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]                        // compose passes "" when ADMIN_BOOTSTRAP_EMAIL is unset
    [InlineData("   ")]
    [InlineData("admin@example.co")]
    [InlineData("someone@example.com")]
    public void IsBootstrapEmail_Should_ReturnFalse_When_EmailIsBlankOrUnlisted(string email)
    {
        Options.IsBootstrapEmail(email).ShouldBeFalse();
    }

    [Fact]
    public void IsBootstrapEmail_Should_ReturnFalse_When_ListIsEmpty()
    {
        new AdminOptions().IsBootstrapEmail("admin@example.com").ShouldBeFalse();
    }
}
