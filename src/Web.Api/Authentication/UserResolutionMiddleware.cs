using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Web.Api.Database;
using Web.Api.Features.Users;

namespace Web.Api.Authentication;

/// <summary>
/// Maps the Clerk <c>sub</c> of an authenticated request to the local user id, once per request, so
/// <see cref="IUserContext.UserId"/> can stay synchronous. Runs after authentication, before authorization.
/// First request of a new Clerk user: provisions the local account from the token's <c>email</c> claim
/// (<see cref="UserProvisioner"/>, 1.4.1). Without that claim the user stays unprovisioned until the
/// Clerk webhook creates them.
/// </summary>
internal sealed class UserResolutionMiddleware(RequestDelegate next, ILogger<UserResolutionMiddleware> logger)
{
    internal const string UserIdItemKey = "codebench.user-id";

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, HybridCache cache, UserProvisioner provisioner)
    {
        string? externalId = context.User.GetExternalId();

        if (externalId is not null)
        {
            Guid? userId = await ResolveAsync(externalId, db, cache, context.RequestAborted)
                ?? await ProvisionAsync(context.User, externalId, provisioner, context.RequestAborted);

            if (userId is not null)
            {
                context.Items[UserIdItemKey] = userId.Value;
            }
        }

        await next(context);
    }

    private async Task<Guid?> ProvisionAsync(
        ClaimsPrincipal principal,
        string externalId,
        UserProvisioner provisioner,
        CancellationToken cancellationToken)
    {
        string? email = principal.FindFirstValue(ClerkClaims.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogWarning(
                "Cannot provision {ExternalId}: the session token has no '{Claim}' claim (customize the Clerk session token, see build plan 1.4)",
                externalId,
                ClerkClaims.Email);
            return null;
        }

        bool emailVerified = bool.TryParse(principal.FindFirstValue(ClerkClaims.EmailVerified), out bool verified) && verified;

        return await provisioner.ProvisionAsync(externalId, email, emailVerified, cancellationToken);
    }

    private static async Task<Guid?> ResolveAsync(
        string externalId,
        ApplicationDbContext db,
        HybridCache cache,
        CancellationToken cancellationToken)
    {
        string key = UserCacheKeys.ByExternalId(externalId);

        Guid? userId = await cache.GetOrCreateAsync(
            key,
            (db, externalId),
            static async (state, ct) => await state.db.Users
                .Where(u => u.ExternalId == state.externalId)
                .Select(u => (Guid?)u.Id)
                .SingleOrDefaultAsync(ct),
            UserCacheKeys.EntryOptions,
            cancellationToken: cancellationToken);

        if (userId is null)
        {
            // Don't remember a miss: the account may be provisioned a moment later (webhook / upsert).
            await cache.RemoveAsync(key, cancellationToken);
        }

        return userId;
    }
}
