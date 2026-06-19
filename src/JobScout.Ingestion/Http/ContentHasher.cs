using System.Security.Cryptography;
using System.Text;

namespace JobScout.Ingestion.Http;

/// <summary>
/// Produces a short stable hash of the fields we treat as "the posting's content", so history
/// can tell an unchanged re-observation from a material edit without storing full job bodies.
/// </summary>
internal static class ContentHasher
{
    public static string Hash(params string?[] parts)
    {
        var joined = string.Join('|', parts.Select(p => p?.Trim() ?? ""));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexStringLower(bytes)[..32];
    }
}
