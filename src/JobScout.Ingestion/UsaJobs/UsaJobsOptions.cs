namespace JobScout.Ingestion.UsaJobs;

/// <summary>
/// Bound from the <c>UsaJobs</c> config section. The USAJOBS API needs a free API key and the
/// registered email as the User-Agent (both from developer.usajobs.gov). Without them the source
/// isn't registered and its feeds are skipped with a log line rather than failing.
/// </summary>
public sealed class UsaJobsOptions
{
    public const string SectionName = "UsaJobs";

    public string? ApiKey { get; set; }

    /// <summary>The email registered with USAJOBS — sent as the User-Agent, as their API requires.</summary>
    public string? UserAgentEmail { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(UserAgentEmail);
}
