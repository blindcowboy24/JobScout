using JobScout.Core.Model;

namespace JobScout.Ingestion.Http;

/// <summary>
/// Normalizes a posting's work arrangement. Prefers a source's structured workplace field; falls
/// back to scanning short, reliable text (title/location — not the full body, which name-drops
/// "remote" in unrelated contexts).
/// </summary>
internal static class RemoteDetector
{
    /// <summary>Maps an ATS "workplace type" string (Lever/Ashby style) to <see cref="RemoteMode"/>.</summary>
    public static RemoteMode FromWorkplaceType(string? workplaceType) => workplaceType?.Trim().ToLowerInvariant() switch
    {
        "remote" => RemoteMode.Remote,
        "hybrid" => RemoteMode.Hybrid,
        "on-site" or "onsite" or "on site" => RemoteMode.Onsite,
        _ => RemoteMode.Unknown,
    };

    /// <summary>Infers from text. Checks hybrid first so "hybrid remote" reads as hybrid.</summary>
    public static RemoteMode FromText(params string?[] parts)
    {
        var s = string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p))).ToLowerInvariant();
        if (s.Length == 0) return RemoteMode.Unknown;
        if (s.Contains("hybrid")) return RemoteMode.Hybrid;
        if (s.Contains("remote") || s.Contains("work from home") || s.Contains("work-from-home") || s.Contains("wfh"))
            return RemoteMode.Remote;
        return RemoteMode.Unknown;
    }
}
