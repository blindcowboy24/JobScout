using System.Net;
using System.Text.RegularExpressions;

namespace JobScout.Ingestion.Http;

/// <summary>
/// Reduces an ATS job body (often HTML, sometimes HTML-escaped) to bounded plain text suitable
/// for keyword search and a short snippet. Not a faithful renderer — just enough to make the
/// tech stack mentioned in the body searchable.
/// </summary>
internal static partial class HtmlText
{
    private const int MaxLength = 4000;

    public static string? ToPlainText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // Greenhouse double-escapes its HTML (e.g. "&lt;p&gt;"); decode twice so tags become real
        // tags we can strip, and entities like &amp; resolve to characters.
        var decoded = WebUtility.HtmlDecode(WebUtility.HtmlDecode(raw));

        var noTags = TagRegex().Replace(decoded, " ");
        var collapsed = WhitespaceRegex().Replace(noTags, " ").Trim();
        if (collapsed.Length == 0) return null;

        return collapsed.Length <= MaxLength ? collapsed : collapsed[..MaxLength];
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
