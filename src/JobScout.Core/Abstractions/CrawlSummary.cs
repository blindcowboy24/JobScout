using JobScout.Core.Model;

namespace JobScout.Core.Abstractions;

/// <summary>What one feed's crawl changed in the store. Returned for logging/observability.</summary>
public sealed record CrawlSummary
{
    public required JobSource Source { get; init; }

    /// <summary>The crawl unit: an ATS board slug, or an aggregator search query/tag.</summary>
    public required string Feed { get; init; }

    /// <summary>Postings seen for the first time ever.</summary>
    public int New { get; init; }

    /// <summary>Postings seen before and still present (whether or not their content changed).</summary>
    public int Updated { get; init; }

    /// <summary>Postings that had vanished and showed up again this crawl (repost churn).</summary>
    public int Reappeared { get; init; }

    /// <summary>Previously-active postings now absent from the feed, marked closed this crawl.</summary>
    public int Closed { get; init; }

    public int TotalObserved => New + Updated + Reappeared;
}
