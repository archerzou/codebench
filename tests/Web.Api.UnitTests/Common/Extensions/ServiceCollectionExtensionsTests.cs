using Microsoft.Extensions.Configuration;
using Web.Api.Common.Extensions;

namespace Web.Api.UnitTests.Common.Extensions;

/// <summary>1.7.3 / SC-04: the CORS list is exact origins only.</summary>
public sealed class ServiceCollectionExtensionsTests
{
    private static IConfiguration Config(params string[] origins) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(origins.Select((o, i) => new KeyValuePair<string, string?>($"Cors:AllowedOrigins:{i}", o)))
            .Build();

    [Fact]
    public void ReadAllowedOrigins_Should_ReturnOrigins_When_AllAreExact()
    {
        string[] origins = ServiceCollectionExtensions.ReadAllowedOrigins(
            Config("http://app.codebench.localhost", "http://localhost:5173", " https://codebench.example "));

        origins.ShouldBe(["http://app.codebench.localhost", "http://localhost:5173", "https://codebench.example"]);
    }

    [Fact]
    public void ReadAllowedOrigins_Should_DropBlankEntries_When_ComposeVariableIsUnset()
    {
        ServiceCollectionExtensions.ReadAllowedOrigins(Config("http://app.codebench.localhost", "", "  "))
            .ShouldBe(["http://app.codebench.localhost"]);
    }

    [Fact]
    public void ReadAllowedOrigins_Should_ReturnEmpty_When_SectionIsMissing()
    {
        ServiceCollectionExtensions.ReadAllowedOrigins(Config()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("*")]
    [InlineData("http://*.codebench.localhost")]
    [InlineData("http://app.codebench.localhost/")]          // trailing slash never matches the Origin header
    [InlineData("http://app.codebench.localhost/path")]
    [InlineData("app.codebench.localhost")]                  // no scheme
    [InlineData("ftp://app.codebench.localhost")]
    [InlineData("http://user:pw@app.codebench.localhost")]
    public void ReadAllowedOrigins_Should_Throw_When_OriginIsNotExact(string origin)
    {
        Should.Throw<InvalidOperationException>(() => ServiceCollectionExtensions.ReadAllowedOrigins(Config(origin)));
    }
}
