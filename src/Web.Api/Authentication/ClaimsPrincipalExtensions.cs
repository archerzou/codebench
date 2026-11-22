using System.Security.Claims;

namespace Web.Api.Authentication;

internal static class ClaimsPrincipalExtensions
{
    /// <summary>The Clerk user id (<c>sub</c>) of an authenticated principal, otherwise null.</summary>
    public static string? GetExternalId(this ClaimsPrincipal? principal) =>
        principal?.Identity?.IsAuthenticated == true
            ? principal.FindFirstValue(ClerkClaims.Subject)
            : null;
}
