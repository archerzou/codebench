using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Web.Api.Authentication;

internal static class AuthenticationExtensions
{
    /// <summary>
    /// Validates Clerk session tokens: signature via the issuer's JWKS, issuer, lifetime and <c>azp</c>
    /// (ADR 0001, SC-02). The API never issues tokens.
    /// </summary>
    internal static IServiceCollection AddClerkAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ClerkOptions>()
            .Bind(configuration.GetSection(ClerkOptions.SectionName))
            .Validate(ClerkOptions.HasValidIssuer, "Clerk:Issuer must be an absolute https URL (the Clerk Frontend API URL).")
            .Validate(ClerkOptions.HasAuthorizedParties, "Clerk:AuthorizedParties must list at least one absolute origin.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<ClerkOptions>>((options, clerkOptions) =>
            {
                ClerkOptions clerk = clerkOptions.Value;

                options.Authority = clerk.Issuer;           // JWKS from {Issuer}/.well-known/openid-configuration
                options.MapInboundClaims = false;           // keep "sub" and "azp" as-is
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = clerk.Issuer,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    // Clerk session tokens carry no "aud" claim; the azp check in OnTokenValidated replaces it (SC-02, ADR 0001).
#pragma warning disable CA5404
                    ValidateAudience = false,
#pragma warning restore CA5404
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClerkClaims.Subject
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        string? azp = context.Principal?.FindFirst(ClerkClaims.AuthorizedParty)?.Value;

                        if (!AuthorizedPartyValidator.IsAllowed(azp, clerk.AuthorizedParties))
                        {
                            context.Fail("Token was not issued for an authorized party.");
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddOptions<AdminOptions>().Bind(configuration.GetSection(AdminOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();
        services.AddScoped<UserProvisioner>();
        services.AddSingleton<ClerkWebhookVerifier>();

        return services;
    }

    /// <summary>Resolves the local user id for authenticated requests. Call between authentication and authorization.</summary>
    internal static IApplicationBuilder UseUserResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<UserResolutionMiddleware>();
}
