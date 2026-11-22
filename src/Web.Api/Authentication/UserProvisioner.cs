using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Web.Api.Common;
using Web.Api.Database;
using Web.Api.Features.Users;

namespace Web.Api.Authentication;

/// <summary>
/// Creates the local user for a Clerk id if it doesn't exist yet — learner by default, admin when the
/// <b>verified</b> email is in <c>Admin:BootstrapEmails</c>. Shared by the first-request upsert
/// (<see cref="UserResolutionMiddleware"/>) and the Clerk webhook, which may race: a duplicate insert is
/// caught on the unique <c>external_id</c> index and the existing row wins.
/// </summary>
internal sealed class UserProvisioner(
    ApplicationDbContext db,
    IDateTimeProvider dateTimeProvider,
    IOptions<AdminOptions> adminOptions,
    ILogger<UserProvisioner> logger)
{
    public async Task<Guid> ProvisionAsync(string externalId, string email, bool emailVerified, CancellationToken cancellationToken)
    {
        Guid? existing = await FindAsync(externalId, cancellationToken);
        if (existing is not null)
        {
            return existing.Value;
        }

        bool bootstrapAdmin = emailVerified && adminOptions.Value.IsBootstrapEmail(email);
        var user = User.Create(externalId, email, bootstrapAdmin ? Role.Admin : Role.Learner, dateTimeProvider.UtcNow);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another request or the webhook created it first.
            db.Entry(user).State = EntityState.Detached;
            return await FindAsync(externalId, cancellationToken)
                ?? throw new InvalidOperationException($"User {externalId} hit a unique violation but cannot be found.", exception);
        }

        if (bootstrapAdmin)
        {
            logger.LogWarning("User {UserId} was given the admin role from Admin:BootstrapEmails", user.Id);
        }

        return user.Id;
    }

    private Task<Guid?> FindAsync(string externalId, CancellationToken cancellationToken) =>
        db.Users
            .Where(u => u.ExternalId == externalId)
            .Select(u => (Guid?)u.Id)
            .SingleOrDefaultAsync(cancellationToken);
}
