using System.Reflection;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Web.Api;
using Web.Api.Authentication;
using Web.Api.Common.Endpoints;
using Web.Api.Common.Extensions;
using Web.Api.Database;
using Web.Api.Database.Seeding;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.Services.AddOpenApiWithAuth();

builder.Services
    .AddApplication()
    .AddPresentation()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddObservability(builder.Configuration, builder.Environment.ApplicationName);

builder.Services.AddRateLimitingInternal(builder.Configuration);

builder.Services.AddCorsInternal(builder.Configuration);

builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

WebApplication app = builder.Build();

app.MapEndpoints();

// One-shot command: migrate and exit (build plan 1.7; Production never migrates on startup — DM-06).
// `--migrate --seed` migrates, then falls through to the seed command below.
if (MigrateCommand.IsRequested(args))
{
    int exitCode = await MigrateCommand.RunAsync(app);
    if (exitCode != 0 || !SeedCommand.IsRequested(args))
    {
        return exitCode;
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApiWithScalar();

    app.ApplyMigrations();
}

// One-shot commands: seed and exit without serving requests (build plan 1.5).
if (SeedCommand.IsRequested(args))
{
    return await SeedCommand.RunAsync(app, args);
}

app.MapHealthChecks("health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.UseSecurityHeaders();

app.UseRequestContextLogging();

app.UseSerilogRequestLogging();

app.UseExceptionHandler();

app.UseCors();

app.UseAuthentication();

app.UseUserResolution();

app.UseAuthorization();

app.UseRateLimiter();

// REMARK: If you want to use Controllers, you'll need this.
app.MapControllers();

await app.RunAsync();

return 0;

// REMARK: Required for functional and integration tests to work.
namespace Web.Api
{
    public partial class Program;
}
