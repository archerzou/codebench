using IntegrationTests.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Testcontainers.Nats;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Web.Api;
using Web.Api.Database;

namespace IntegrationTests;

public sealed class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("codebench")
        .WithUsername("codebench")
        .WithPassword("codebench")
        .Build();

    // Real Redis (HybridCache L2) and NATS, so /health and the cache run as in production (1.7).
    private readonly RedisContainer _redisContainer = new RedisBuilder("redis:7-alpine").Build();

    private readonly NatsContainer _natsContainer = new NatsBuilder("nats:2").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", _dbContainer.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", _redisContainer.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Nats", _natsContainer.GetConnectionString());

        // Clerk is replaced by a test issuer + RSA key (TS-04). The real JwtBearer pipeline and the
        // azp check still run; only the source of signing keys changes (no JWKS download).
        builder.UseSetting("Clerk:Issuer", TestJwt.Issuer);
        builder.UseSetting("Clerk:AuthorizedParties:0", TestJwt.AuthorizedParty);
        builder.UseSetting("Clerk:WebhookSecret", TestWebhook.Secret);
        builder.UseSetting("Admin:BootstrapEmails:0", TestWebhook.BootstrapEmail);

        // Relax rate limiting so the test suite is not throttled.
        builder.UseSetting("RateLimiting:Global:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Authentication:PermitLimit", "100000");

        builder.ConfigureTestServices(services =>
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = TestJwt.Issuer };
                configuration.SigningKeys.Add(TestJwt.SigningKey);

                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            }));
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_dbContainer.StartAsync(), _redisContainer.StartAsync(), _natsContainer.StartAsync());

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _dbContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
        await _natsContainer.DisposeAsync();
    }
}
