namespace JobScout.Data.Entities;

/// <summary>
/// One observation of a tracked posting at a single crawl. The append-only history that the
/// timeline fields on <see cref="TrackedPosting"/> are rolled up from — kept so the evidence
/// behind a score (when it appeared, vanished, changed) stays auditable.
/// </summary>
public class PostingSnapshot
{
    public long Id { get; set; }

    public int TrackedPostingId { get; set; }
    public TrackedPosting? TrackedPosting { get; set; }

    public DateTimeOffset ObservedAt { get; set; }

    /// <summary>True if the posting was present in the feed at this crawl; false if it had gone.</summary>
    public bool WasPresent { get; set; }

    /// <summary>True when the content hash differed from the prior observation.</summary>
    public bool ContentChanged { get; set; }

    public string? ContentHash { get; set; }

    /// <summary>The intent score computed at this crawl, for trend analysis over time.</summary>
    public double Score { get; set; }
}
