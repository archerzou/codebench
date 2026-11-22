using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IntegrationTests.Authentication;

/// <summary>
/// Mints Clerk-shaped session tokens (iss, sub, azp, exp) signed with a test RSA key, so integration
/// tests exercise the real JwtBearer validation and azp check without Clerk (TS-04).
/// </summary>
internal static class TestJwt
{
    public const string Issuer = "https://clerk.codebench.test";

    public const string AuthorizedParty = "http://app.codebench.localhost";

    private static readonly RSA Rsa = RSA.Create(2048);

    public static SecurityKey SigningKey { get; } = new RsaSecurityKey(Rsa) { KeyId = "codebench-test" };

    public static string Create(
        string subject,
        string? authorizedParty = AuthorizedParty,
        string issuer = Issuer,
        DateTime? expiresAt = null,
        SecurityKey? signingKey = null,
        string? email = null,
        bool? emailVerified = null)
    {
        DateTime expires = expiresAt ?? DateTime.UtcNow.AddMinutes(5);

        var claims = new Dictionary<string, object> { ["sub"] = subject };
        if (authorizedParty is not null)
        {
            claims["azp"] = authorizedParty;
        }

        // Custom session-token claims (Clerk dashboard → Sessions → Customize session token).
        if (email is not null)
        {
            claims["email"] = email;
        }

        if (emailVerified is not null)
        {
            claims["email_verified"] = emailVerified.Value;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Claims = claims,
            IssuedAt = expires.AddMinutes(-10),
            NotBefore = expires.AddMinutes(-10),
            Expires = expires,
            SigningCredentials = new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.RsaSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>A key nobody trusts, for "signed by someone else" cases.</summary>
    public static SecurityKey CreateUntrustedKey()
    {
        using var rsa = RSA.Create(2048);
        return new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: true)) { KeyId = "untrusted" };
    }
}
