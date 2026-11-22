namespace Web.Api.Authentication;

/// <summary>
/// Clerk session tokens carry no audience; <c>azp</c> is what stops a token minted for another site
/// (sharing the same Clerk instance or a lookalike) from being accepted here (SC-02).
/// </summary>
internal static class AuthorizedPartyValidator
{
    public static bool IsAllowed(string? authorizedParty, IEnumerable<string> allowedParties)
    {
        if (string.IsNullOrWhiteSpace(authorizedParty))
        {
            return false;
        }

        string candidate = Normalize(authorizedParty);

        return allowedParties.Any(allowed => string.Equals(Normalize(allowed), candidate, StringComparison.OrdinalIgnoreCase));
    }

    // Origins never have a path; tolerate a trailing slash in configuration.
    private static string Normalize(string origin) => origin.Trim().TrimEnd('/');
}
