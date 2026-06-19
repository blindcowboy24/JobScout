using JobScout.Core.Model;
using JobScout.Core.Scoring;

namespace JobScout.Data.Entities;

/// <summary>
/// The durable, deduplicated record of one posting across all the times we've crawled it.
/// Identity is (Source, Company, ExternalId). The timeline fields here are what make the
/// time-based intent signals possible — they only mean anything after repeated crawls.
/// </summary>
public class TrackedPosting
{
    public int Id { get; set; }

    public JobSource Source { get; set; }

    /// <summary>The crawl unit this posting was found through: an ATS board slug or aggregator query/tag.</summary>
    public string Feed { get; set; } = "";

    /// <summary>The hiring company (display). Equals <see cref="Feed"/> for company ATS sources.</summary>
    public string Company { get; set; } = "";

    public string ExternalId { get; set; } = "";

    public string Title { get; set; } = "";
    public string? Location { get; set; }
    public string? Department { get; set; }
    public string? Url { get; set; }

    public RemoteMode Remote { get; set; }

    /// <summary>Plain-text description (bounded) — the searchable body where the tech stack lives.</summary>
    public string? Description { get; set; }

    public DateTimeOffset? PostedAt { get; set; }
    public DateTimeOffset? SourceUpdatedAt { get; set; }
    public DateOnly? ApplicationDeadline { get; set; }

    public bool? HasSalaryBand { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string? SalaryCurrency { get; set; }
    public string? SalaryInterval { get; set; }

    public bool? HasScreeningQuestions { get; set; }

    public string? ContentHash { get; set; }

    // --- Timeline: the signal-bearing part ---

    /// <summary>First crawl in which we ever saw this posting.</summary>
    public DateTimeOffset FirstSeen { get; set; }

    /// <summary>Most recent crawl in which the posting was present in the feed.</summary>
    public DateTimeOffset LastSeen { get; set; }

    /// <summary>Whether the posting was present in the most recent crawl of its board.</summary>
    public bool IsActive { get; set; }

    /// <summary>Total crawls in which the posting was present.</summary>
    public int ObservationCount { get; set; }

    /// <summary>Times the posting vanished from the feed and later returned.</summary>
    public int RepostCount { get; set; }

    // --- Latest score (recomputed each crawl that touches the posting) ---

    public double Score { get; set; }
    public IntentBand Band { get; set; }
    public DateTimeOffset? ScoredAt { get; set; }

    public List<PostingSnapshot> Snapshots { get; set; } = [];
}
