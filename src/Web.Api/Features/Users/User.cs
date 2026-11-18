using Web.Api.Common;

namespace Web.Api.Features.Users;

/// <summary>
/// A CodeBench account. Identity lives in Clerk; <see cref="ExternalId"/> is the Clerk user id (the
/// token's <c>sub</c>). Role, plan and flags are added in build plan step 1.2.
/// </summary>
public sealed class User : Entity
{
    private User()
    {
    }

    public Guid Id { get; private set; }

    public string ExternalId { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public static User Create(string externalId, string email, DateTime createdAt) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            ExternalId = externalId,
            Email = email,
            CreatedAt = createdAt
        };

    public void ChangeEmail(string email) => Email = email;
}
