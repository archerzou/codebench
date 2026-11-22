using Web.Api.Common;

namespace Web.Api.Features.Users;

/// <summary>
/// A CodeBench account. Identity lives in Clerk; <see cref="ExternalId"/> is the Clerk user id (the
/// token's <c>sub</c>). Authorization lives here: <see cref="Role"/> (→ permissions) and
/// <see cref="IsDisabled"/> (SC-03). Demo users (<see cref="IsDemo"/>, <c>ExternalId = "seed|…"</c>) exist
/// only in Development (SC-05).
/// </summary>
public sealed class User : Entity
{
    /// <summary>Prefix of demo users' <see cref="ExternalId"/>; never a valid Clerk id.</summary>
    public const string DemoExternalIdPrefix = "seed|";

    private User()
    {
    }

    public Guid Id { get; private set; }

    public string ExternalId { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public Role Role { get; private set; } = Role.Learner;

    public Plan Plan { get; private set; } = Plan.Free;

    public bool IsDisabled { get; private set; }

    public bool IsDemo { get; private set; }

    public DateTime CreatedAt { get; private set; }

    /// <summary>Set when the Clerk account was deleted (webhook). The row stays for submission history.</summary>
    public DateTime? DeletedAt { get; private set; }

    public static User Create(string externalId, string email, Role role, DateTime createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            ExternalId = externalId,
            Email = email,
            Role = role,
            CreatedAt = createdAt
        };
        user.Raise(new UserCreatedDomainEvent(user.Id, user.Role));

        return user;
    }

    /// <summary>A seeded demo learner (guide 09): cannot sign in (no Clerk account), wiped by <c>IsDemo</c>.</summary>
    public static User CreateDemo(string name, string email, Role role, DateTime createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        User user = Create($"{DemoExternalIdPrefix}{name}", email, role, createdAt);
        user.IsDemo = true;

        return user;
    }

    public void ChangeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        Email = email;
    }

    /// <summary>Idempotent; callers invalidate <c>permissions:{externalId}</c> (QY-05).</summary>
    public void ChangeRole(Role role)
    {
        if (Role == role)
        {
            return;
        }

        Role = role;
        Raise(new UserRoleChangedDomainEvent(Id, ExternalId, role));
    }

    /// <summary>Set by the payment webhook (Phase 7); Pro unlocks non-free problems.</summary>
    public void ChangePlan(Plan plan) => Plan = plan;

    public void Disable() => IsDisabled = true;

    /// <summary>Re-enables an admin-disabled account. A deleted account stays disabled (409).</summary>
    public Result Enable()
    {
        if (DeletedAt is not null)
        {
            return Result.Failure(UserErrors.Deleted(Id));
        }

        IsDisabled = false;
        return Result.Success();
    }

    /// <summary>
    /// Soft delete after the Clerk account is deleted: disabled for good, email anonymised, row kept so
    /// submissions and audit history stay intact. Idempotent.
    /// </summary>
    public void MarkDeleted(DateTime deletedAt)
    {
        if (DeletedAt is not null)
        {
            return;
        }

        DeletedAt = deletedAt;
        IsDisabled = true;
        Email = $"deleted-{Id:N}@users.codebench.invalid";
        Raise(new UserDeletedDomainEvent(Id, ExternalId));
    }
}
