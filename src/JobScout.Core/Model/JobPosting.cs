namespace JobScout.Core.Model;

/// <summary>
/// A canonical, source-agnostic snapshot of a single posting as observed in one crawl.
/// Ingestion clients map each ATS's native shape onto this; everything downstream
/// (history tracking, scoring) speaks only this type.
/// </summary>
public sealed record JobPosting
{
    /// <summary>The ATS this posting came from.</summary>
    public required JobSource Source { get; init; }

    /// <summary>
    /// The hiring company. For a company ATS this is effectively the board slug we queried; for an
    /// aggregator it's the employer name carried in the posting. (The crawl <em>feed</em> — which
    /// may be a search query — is tracked separately by the data layer, not here.)
    /// </summary>
    public required string Company { get; init; }

    /// <summary>The posting's stable id <em>within its source</em>. Unique only per (Source, Company).</summary>
    public required string ExternalId { get; init; }

    public required string Title { get; init; }

    public string? Location { get; init; }
    public string? Department { get; init; }

    /// <summary>Remote/hybrid/on-site, from the source's structured flag or inferred from text.</summary>
    public RemoteMode Remote { get; init; } = RemoteMode.Unknown;

    /// <summary>
    /// Plain-text job description (HTML stripped, whitespace-collapsed, truncated). The tech stack
    /// lives here, not in the title — so this is what makes a search like ".NET" or "C#" actually
    /// find roles. Kept bounded; it's for keyword matching and a snippet, not full reproduction.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>Canonical URL a human would open to read/apply.</summary>
    public string? Url { get; init; }

    /// <summary>When the ATS says the posting first went live, if disclosed.</summary>
    public DateTimeOffset? PostedAt { get; init; }

    /// <summary>When the ATS last touched the posting, if disclosed. Useful as a freshness hint.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Application close date, if disclosed.</summary>
    public DateOnly? ApplicationDeadline { get; init; }

    /// <summary>Disclosed pay range, if any.</summary>
    public SalaryBand? Salary { get; init; }

    /// <summary>
    /// Whether the posting carries custom screening questions, when the source exposes it.
    /// Null means "the source's list endpoint doesn't tell us" — distinct from a known false.
    /// </summary>
    public bool? HasScreeningQuestions { get; init; }

    /// <summary>
    /// A stable hash of the content fields we track for change detection. Lets history
    /// distinguish "seen again, unchanged" from "materially edited" without storing full bodies.
    /// </summary>
    public string? ContentHash { get; init; }
}
