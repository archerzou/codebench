using System.Net;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// 1.7.3 / SC-04: a browser preflight from a listed origin is allowed (with credentials); any other origin gets no
/// CORS headers. The factory runs in Development, so the list is <c>appsettings.Development.json</c>'s.
/// </summary>
public sealed class CorsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Theory]
    [InlineData("http://app.codebench.localhost")]
    [InlineData("http://codebench.localhost")]
    [InlineData("http://localhost:5173")]
    [InlineData("http://localhost:4321")]
    public async Task Preflight_Should_AllowOrigin_When_OriginIsListed(string origin)
    {
        using HttpRequestMessage request = Preflight(origin);
        using HttpResponseMessage response = await HttpClient.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe([origin]);
        response.Headers.GetValues("Access-Control-Allow-Credentials").ShouldBe(["true"]);
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("http://app.codebench.localhost.evil.example")]
    [InlineData("https://app.codebench.localhost")]   // scheme is part of the origin
    public async Task Preflight_Should_NotAllowOrigin_When_OriginIsNotListed(string origin)
    {
        using HttpRequestMessage request = Preflight(origin);
        using HttpResponseMessage response = await HttpClient.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "problems");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");
        return request;
    }
}
