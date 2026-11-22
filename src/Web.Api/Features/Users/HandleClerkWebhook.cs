using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Web.Api.Authentication;
using Web.Api.Authorization;
using Web.Api.Common;
using Web.Api.Common.Endpoints;
using Web.Api.Common.Extensions;
using Web.Api.Common.Messaging;
using Web.Api.Database;

namespace Web.Api.Features.Users;

/// <summary>
/// <c>POST /webhooks/clerk</c> — keeps local users in sync with Clerk (<c>user.created</c>,
/// <c>user.updated</c>, <c>user.deleted</c>). The endpoint verifies the Svix signature on the raw body before
/// parsing anything (EP-03 webhook exception, SC-02); the handler applies the event idempotently, because Svix
/// delivers at least once and in no particular order. Other event types are acknowledged and ignored.
/// </summary>
public static class HandleClerkWebhook
{
    public const string UserCreated = "user.created";
    public const string UserUpdated = "user.updated";
    public const string UserDeleted = "user.deleted";

    public sealed record Command(string EventType, string ExternalId, string? Email, bool EmailVerified) : ICommand;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.EventType).NotEmpty().MaximumLength(100);
            RuleFor(c => c.ExternalId).NotEmpty().MaximumLength(64);
            RuleFor(c => c.Email).EmailAddress().MaximumLength(320).When(c => c.Email is not null);
        }
    }

    internal sealed class Handler(
        ApplicationDbContext db,
        UserProvisioner provisioner,
        HybridCache cache,
        IDateTimeProvider dateTimeProvider)
        : ICommandHandler<Command>
    {
        public async Task<Result> Handle(Command command, CancellationToken cancellationToken) =>
            command.EventType switch
            {
                UserCreated or UserUpdated => await UpsertAsync(command, cancellationToken),
                UserDeleted => await DeleteAsync(command.ExternalId, cancellationToken),
                _ => Result.Success()
            };

        private async Task<Result> UpsertAsync(Command command, CancellationToken cancellationToken)
        {
            User? user = await db.Users.SingleOrDefaultAsync(u => u.ExternalId == command.ExternalId, cancellationToken);

            if (user is null)
            {
                // Out-of-order "updated" before "created" provisions too; a user without an email can't be stored.
                if (command.Email is not null)
                {
                    await provisioner.ProvisionAsync(command.ExternalId, command.Email, command.EmailVerified, cancellationToken);
                }

                return Result.Success();
            }

            if (user.DeletedAt is null && command.Email is not null && !string.Equals(user.Email, command.Email, StringComparison.Ordinal))
            {
                user.ChangeEmail(command.Email);
                await db.SaveChangesAsync(cancellationToken);
            }

            return Result.Success();
        }

        private async Task<Result> DeleteAsync(string externalId, CancellationToken cancellationToken)
        {
            User? user = await db.Users.SingleOrDefaultAsync(u => u.ExternalId == externalId, cancellationToken);
            if (user is null)
            {
                return Result.Success();
            }

            user.MarkDeleted(dateTimeProvider.UtcNow);
            await db.SaveChangesAsync(cancellationToken);

            await cache.RemoveAsync(UserCacheKeys.ByExternalId(externalId), cancellationToken);
            await cache.RemoveAsync(PermissionCacheKeys.ForUser(externalId), cancellationToken);

            return Result.Success();
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        /// <summary>Clerk payloads are a few KB; anything far larger is not a Clerk event.</summary>
        private const long MaxBodyBytes = 256 * 1024;

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

        /// <summary>Envelope: <c>{ type, data, object, timestamp, instance_id }</c>.</summary>
        public sealed record Payload(string? Type, PayloadUser? Data);

        /// <summary><c>data</c> for user events; <c>user.deleted</c> carries only <c>id</c> (+ <c>deleted</c>).</summary>
        public sealed record PayloadUser(string? Id, IReadOnlyList<PayloadEmail>? EmailAddresses, string? PrimaryEmailAddressId);

        public sealed record PayloadEmail(string? Id, string? EmailAddress, PayloadVerification? Verification);

        public sealed record PayloadVerification(string? Status);

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("webhooks/clerk", async (
                HttpRequest request,
                ClerkWebhookVerifier verifier,
                ICommandHandler<Command> handler,
                CancellationToken cancellationToken) =>
            {
                using var reader = new StreamReader(request.Body);
                string body = await reader.ReadToEndAsync(cancellationToken);

                WebhookVerification verification = verifier.Verify(body, request.Headers);
                if (verification != WebhookVerification.Valid)
                {
                    return CustomResults.Problem(verification == WebhookVerification.NotConfigured
                        ? UserErrors.WebhookNotConfigured
                        : UserErrors.InvalidWebhookSignature);
                }

                Command? command = ToCommand(body);
                if (command is null)
                {
                    return CustomResults.Problem(UserErrors.InvalidWebhookPayload);
                }

                Result result = await handler.Handle(command, cancellationToken);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .AllowAnonymous()
            .ExcludeFromDescription()                 // internal to Clerk: no operationId / response docs
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes))
            .WithTags(Tags.Users);
        }

        private static Command? ToCommand(string body)
        {
            Payload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<Payload>(body, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }

            if (payload?.Type is not { Length: > 0 } type || payload.Data?.Id is not { Length: > 0 } externalId)
            {
                return null;
            }

            PayloadEmail? primary = payload.Data.EmailAddresses?
                .FirstOrDefault(e => e.Id is not null && e.Id == payload.Data.PrimaryEmailAddressId);

            return new Command(
                type,
                externalId,
                string.IsNullOrWhiteSpace(primary?.EmailAddress) ? null : primary.EmailAddress,
                string.Equals(primary?.Verification?.Status, "verified", StringComparison.Ordinal));
        }
    }
}
