namespace Web.Api.Authentication;

/// <summary>Admin settings (section <c>Admin</c>). Compose maps <c>ADMIN_BOOTSTRAP_EMAIL</c> → <c>Admin__BootstrapEmails__0</c>.</summary>
public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>
    /// A user created (first request or webhook) with one of these <b>verified</b> emails gets the admin
    /// role (guide 09). Only at creation — existing users are never promoted by this list.
    /// </summary>
    public IReadOnlyList<string> BootstrapEmails { get; init; } = [];

    /// <summary>Optional key protecting admin operations if ever exposed over HTTP.</summary>
    public string ApiKey { get; init; } = string.Empty;

    internal bool IsBootstrapEmail(string email) =>
        !string.IsNullOrWhiteSpace(email) &&
        BootstrapEmails.Any(candidate =>
            !string.IsNullOrWhiteSpace(candidate) &&
            string.Equals(candidate.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase));
}
