using Web.Api.Authentication;

namespace Web.Api.UnitTests.Authentication;

public sealed class AuthorizedPartyValidatorTests
{
    private static readonly string[] Allowed = ["http://app.codebench.localhost", "http://localhost:5173/"];

    [Theory]
    [InlineData("http://app.codebench.localhost")]
    [InlineData("http://app.codebench.localhost/")]
    [InlineData("HTTP://APP.CODEBENCH.LOCALHOST")]
    [InlineData("http://localhost:5173")]
    public void IsAllowed_Should_ReturnTrue_When_PartyIsConfigured(string azp)
    {
        AuthorizedPartyValidator.IsAllowed(azp, Allowed).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://app.codebench.localhost")]       // scheme differs
    [InlineData("http://app.codebench.localhost.evil.io")] // suffix trick
    [InlineData("http://localhost:5174")]                 // port differs
    public void IsAllowed_Should_ReturnFalse_When_PartyIsMissingOrUnknown(string? azp)
    {
        AuthorizedPartyValidator.IsAllowed(azp, Allowed).ShouldBeFalse();
    }

    [Fact]
    public void IsAllowed_Should_ReturnFalse_When_NothingIsConfigured()
    {
        AuthorizedPartyValidator.IsAllowed("http://app.codebench.localhost", []).ShouldBeFalse();
    }
}
