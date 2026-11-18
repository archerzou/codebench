namespace Web.Api.Authentication;

/// <summary>Claim names in a Clerk session token (inbound claim mapping is off).</summary>
public static class ClerkClaims
{
    /// <summary>Clerk user id — our <c>User.ExternalId</c>.</summary>
    public const string Subject = "sub";

    /// <summary>Authorized party: the origin the token was minted for.</summary>
    public const string AuthorizedParty = "azp";
}
