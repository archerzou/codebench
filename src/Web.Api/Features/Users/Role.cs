namespace Web.Api.Features.Users;

/// <summary>A user's role. Stored as text; the role → permission matrix lives in <c>role_permissions</c> (SC-03).</summary>
public enum Role
{
    Learner,
    Author,
    Admin
}
