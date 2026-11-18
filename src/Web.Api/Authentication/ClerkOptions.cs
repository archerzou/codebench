namespace Web.Api.Authentication;

/// <summary>Clerk settings (section <c>Clerk</c>). Validated at startup (SC-06).</summary>
public sealed class ClerkOptions
{
    public const string SectionName = "Clerk";

    /// <summary>Frontend API URL, e.g. <c>https://&lt;name&gt;.clerk.accounts.dev</c>. Token issuer + JWKS host.</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Origins allowed in the token's <c>azp</c> claim (the app's URLs).</summary>
    public IReadOnlyList<string> AuthorizedParties { get; init; } = [];

    /// <summary>Svix signing secret for <c>POST /webhooks/clerk</c> (step 1.4).</summary>
    public string WebhookSecret { get; init; } = string.Empty;

    /// <summary>Backend API key; only needed if the API calls Clerk's Backend API.</summary>
    public string SecretKey { get; init; } = string.Empty;

    // The placeholder shipped in docs and .env.example; catching it here beats a JWKS failure on the first request.
    private const string Placeholder = "YOUR-INSTANCE";

    internal static bool HasValidIssuer(ClerkOptions options) =>
        Uri.TryCreate(options.Issuer, UriKind.Absolute, out Uri? issuer) &&
        issuer.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(issuer.Query) &&
        !options.Issuer.Contains(Placeholder, StringComparison.OrdinalIgnoreCase);

    internal static bool HasAuthorizedParties(ClerkOptions options) =>
        options.AuthorizedParties.Count > 0 &&
        options.AuthorizedParties.All(IsOrigin);

    // "localhost:5173" parses as an absolute URI with scheme "localhost"; require a real http(s) origin.
    private static bool IsOrigin(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
        uri.AbsolutePath == "/" &&
        string.IsNullOrEmpty(uri.Query);
}
