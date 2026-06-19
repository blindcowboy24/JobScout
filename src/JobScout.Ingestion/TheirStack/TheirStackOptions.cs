namespace JobScout.Ingestion.TheirStack;

/// <summary>
/// Bound from the <c>TheirStack</c> config section. TheirStack needs an API key
/// (api.theirstack.com). Without it the source isn't registered and its feeds are skipped with a
/// log line rather than failing.
/// </summary>
public sealed class TheirStackOptions
{
    public const string SectionName = "TheirStack";

    public string? ApiKey { get; set; }

    /// <summary>Only return postings first seen within this many days (keeps the feed fresh).</summary>
    public int MaxAgeDays { get; set; } = 30;

    /// <summary>Max postings per feed per crawl.</summary>
    public int Limit { get; set; } = 50;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
