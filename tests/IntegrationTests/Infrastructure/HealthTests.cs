using System.Net;
using System.Text.Json;

namespace IntegrationTests.Infrastructure;

/// <summary>1.7.2: GET /health covers Postgres, Redis and NATS (real containers) and is anonymous.</summary>
public sealed class HealthTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Health_Should_ReportHealthy_When_PostgresRedisAndNatsAreUp()
    {
        HttpResponseMessage response = await HttpClient.GetAsync("health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var report = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        report.RootElement.GetProperty("status").GetString().ShouldBe("Healthy");

        JsonElement entries = report.RootElement.GetProperty("entries");
        foreach (string check in new[] { "postgres", "redis", "nats" })
        {
            entries.GetProperty(check).GetProperty("status").GetString().ShouldBe("Healthy", check);
        }
    }
}
