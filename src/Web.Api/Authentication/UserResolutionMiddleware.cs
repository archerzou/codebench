using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Web.Api.Database;
using Web.Api.Features.Users;

namespace Web.Api.Authentication;

/// <summary>
/// Maps the Clerk <c>sub</c> of an authenticated request to the local user id, once per request, so
/// <see cref="IUserContext.UserId"/> can stay synchronous. Runs after authentication, before authorization.
/// Step 1.4.1 extends this to create the user on first request (upsert).
/// </summary>
internal sealed class UserResolutionMiddleware(RequestDelegate next)
{
    internal const string UserIdItemKey = "codebench.user-id";

    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = UserCacheKeys.ByExternalIdExpiration,
        LocalCacheExpiration = UserCacheKeys.ByExternalIdExpiration
    };

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, HybridCache cache)
    {
        string? externalId = context.User.GetExternalId();

        if (externalId is not null)
        {
            Guid? userId = await ResolveAsync(externalId, db, cache, context.RequestAborted);

            if (userId is not null)
            {
                context.Items[UserIdItemKey] = userId.Value;
            }
        }

        await next(context);
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
            CacheOptions,
            cancellationToken: cancellationToken);

        if (userId is null)
        {
            // Don't remember a miss: the account may be provisioned a moment later (webhook / upsert).
            await cache.RemoveAsync(key, cancellationToken);
        }

        return userId;
    }
}
