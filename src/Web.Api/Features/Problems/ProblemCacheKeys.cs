using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Hybrid;

namespace Web.Api.Features.Problems;

/// <summary>HybridCache keys and entry options owned by the Problems feature (QY-05). Every entry carries <see cref="Tag"/>.</summary>
public static class ProblemCacheKeys
{
    /// <summary>On every <c>problems:*</c> entry; cleared by seeding, Publish, Archive and Update.</summary>
    public const string Tag = "problems";

    public static readonly TimeSpan Expiration = TimeSpan.FromMinutes(10);

    public static readonly HybridCacheEntryOptions EntryOptions = new()
    {
        Expiration = Expiration,
        LocalCacheExpiration = Expiration
    };

    public static readonly IReadOnlyCollection<string> Tags = [Tag];

    /// <summary><c>problems:list:{hash}</c> — one entry per normalised catalog query (filters + sort + page).</summary>
    public static string List(string normalizedQuery)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedQuery));
        return $"problems:list:{Convert.ToHexStringLower(hash.AsSpan(0, 8))}";
    }

    /// <summary><c>problems:{slug}</c> — the caller-independent detail of a Published problem.</summary>
    public static string BySlug(string slug) => $"problems:{slug}";
}
