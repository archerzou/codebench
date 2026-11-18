using Microsoft.EntityFrameworkCore;
using Web.Api.Authentication;
using Web.Api.Authorization;
using Web.Api.Common;
using Web.Api.Common.Endpoints;
using Web.Api.Common.Extensions;
using Web.Api.Common.Messaging;
using Web.Api.Database;

namespace Web.Api.Features.Users;

/// <summary>
/// <c>GET /me</c> — the signed-in user's account and permissions (drives <c>RequirePermission</c> in the UI).
/// Role and plan are added with the role matrix (build plan 1.2–1.3).
/// </summary>
public static class GetMe
{
    public sealed record Query : IQuery<Response>;

    public sealed record Response
    {
        public required Guid Id { get; init; }

        public required string Email { get; init; }

        public required IReadOnlyList<string> Permissions { get; init; }
    }

    internal sealed class Handler(
        ApplicationDbContext db,
        IUserContext userContext,
        PermissionProvider permissionProvider)
        : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            if (!userContext.TryGetUserId(out Guid userId))
            {
                return Result.Failure<Response>(UserErrors.NotProvisioned);
            }

            var user = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => new { u.Id, u.Email })
                .SingleOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return Result.Failure<Response>(UserErrors.NotFound(userId));
            }

            IReadOnlySet<string> permissions =
                await permissionProvider.GetForExternalIdAsync(userContext.ExternalId, cancellationToken);

            return new Response
            {
                Id = user.Id,
                Email = user.Email,
                Permissions = [.. permissions.Order(StringComparer.Ordinal)]
            };
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("me", async (IQueryHandler<Query, Response> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new Query(), cancellationToken)).Match(Results.Ok, CustomResults.Problem))
            .RequireAuthorization()
            .WithTags(Tags.Users);
        }
    }
}
