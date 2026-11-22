using Microsoft.AspNetCore.OpenApi;

namespace Web.Api.Common.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddOpenApiWithAuth(this IServiceCollection services)
    {
        services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

        return services;
    }

    /// <summary>
    /// SC-04: an explicit origin list from <c>Cors:AllowedOrigins</c> (app + marketing; localhost dev ports in
    /// Development). Blank entries (an unset compose variable) are dropped; a wildcard or a malformed origin fails
    /// startup, because a browser would silently never match it.
    /// </summary>
    internal static IServiceCollection AddCorsInternal(this IServiceCollection services, IConfiguration configuration)
    {
        string[] origins = ReadAllowedOrigins(configuration);

        services.AddCors(options => options.AddDefaultPolicy(policy =>
            policy
                .WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));

        return services;
    }

    internal static string[] ReadAllowedOrigins(IConfiguration configuration)
    {
        string[] configured = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        string[] origins = [.. configured.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim())];

        foreach (string origin in origins)
        {
            if (!IsValidOrigin(origin))
            {
                throw new InvalidOperationException(
                    $"Cors:AllowedOrigins contains '{origin}'. Use exact origins such as 'http://app.codebench.localhost' " +
                    "(scheme + host [+ port], no path, no trailing slash, no wildcard).");
            }
        }

        return origins;
    }

    private static bool IsValidOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && string.IsNullOrEmpty(uri.UserInfo)
        && !origin.Contains('*', StringComparison.Ordinal)
        && string.Equals(uri.GetLeftPart(UriPartial.Authority), origin, StringComparison.OrdinalIgnoreCase);
}
