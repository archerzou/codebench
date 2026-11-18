using Web.Api.Authentication;

namespace Web.Api.UnitTests.Authentication;

public sealed class ClerkOptionsTests
{
    [Fact]
    public void HasValidIssuer_Should_ReturnTrue_When_IssuerIsHttpsUrl()
    {
        var options = new ClerkOptions { Issuer = "https://example-1234.clerk.accounts.dev" };

        ClerkOptions.HasValidIssuer(options).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://YOUR-INSTANCE.clerk.accounts.dev")] // the docs/.env.example placeholder
    [InlineData("http://example.clerk.accounts.dev")]        // not https
    [InlineData("example.clerk.accounts.dev")]               // not absolute
    [InlineData("https://example.clerk.accounts.dev?x=1")]   // query string
    public void HasValidIssuer_Should_ReturnFalse_When_IssuerIsMissingPlaceholderOrNotHttps(string issuer)
    {
        var options = new ClerkOptions { Issuer = issuer };

        ClerkOptions.HasValidIssuer(options).ShouldBeFalse();
    }

    [Fact]
    public void HasAuthorizedParties_Should_ReturnFalse_When_ListIsEmpty()
    {
        ClerkOptions.HasAuthorizedParties(new ClerkOptions()).ShouldBeFalse();
    }

    [Fact]
    public void HasAuthorizedParties_Should_ReturnFalse_When_EntryIsNotAnAbsoluteUrl()
    {
        var options = new ClerkOptions { AuthorizedParties = ["http://app.codebench.localhost", "localhost:5173"] };

        ClerkOptions.HasAuthorizedParties(options).ShouldBeFalse();
    }

    [Fact]
    public void HasAuthorizedParties_Should_ReturnTrue_When_AllEntriesAreOrigins()
    {
        var options = new ClerkOptions { AuthorizedParties = ["http://app.codebench.localhost", "http://localhost:5173"] };

        ClerkOptions.HasAuthorizedParties(options).ShouldBeTrue();
    }
}
