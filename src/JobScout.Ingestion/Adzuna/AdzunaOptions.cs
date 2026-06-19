namespace JobScout.Ingestion.Adzuna;

/// <summary>
/// Bound from the <c>Adzuna</c> config section. The Adzuna API needs a free app id + key
/// (register at developer.adzuna.com); without them the Adzuna source is simply not registered,
/// and configured Adzuna feeds are skipped with a log line rather than failing.
/// </summary>
public sealed class AdzunaOptions
{
    public const string SectionName = "Adzuna";

    /// <summary>ISO country code for the Adzuna market to search (e.g. <c>us</c>, <c>gb</c>).</summary>
    public string Country { get; set; } = "us";

    public string? AppId { get; set; }
    public string? AppKey { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(AppKey);
}
