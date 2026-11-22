namespace Web.Api.Authentication;

/// <summary>Claim names in a Clerk session token (inbound claim mapping is off).</summary>
public static class ClerkClaims
{
    /// <summary>Clerk user id — our <c>User.ExternalId</c>.</summary>
    public const string Subject = "sub";

    /// <summary>Authorized party: the origin the token was minted for.</summary>
    public const string AuthorizedParty = "azp";

    /// <summary>
    /// Custom claim (Clerk dashboard → Sessions → Customize session token):
    /// <c>"email": "{{user.primary_email_address}}"</c>. Needed to provision a user on first request (1.4).
    /// </summary>
    public const string Email = "email";

    /// <summary>
    /// Custom claim <c>"email_verified": "{{user.email_verified}}"</c>. Bootstrap-admin requires it to be
    /// true; a missing claim counts as unverified (fail closed).
    /// </summary>
    public const string EmailVerified = "email_verified";
}
