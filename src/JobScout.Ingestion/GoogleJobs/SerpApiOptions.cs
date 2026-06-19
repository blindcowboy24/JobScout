namespace JobScout.Ingestion.GoogleJobs;

/// <summary>
/// Bound from the <c>SerpApi</c> config section. Google Jobs is queried through SerpApi, which
/// needs an API key (serpapi.com). Without it the GoogleJobs source isn't registered and its
/// feeds are skipped with a log line rather than failing.
/// </summary>
public sealed class SerpApiOptions
{
    public const string SectionName = "SerpApi";

    public string? ApiKey { get; set; }

    /// <summary>Optional location hint passed to Google Jobs (e.g. <c>United States</c>).</summary>
    public string? Location { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
