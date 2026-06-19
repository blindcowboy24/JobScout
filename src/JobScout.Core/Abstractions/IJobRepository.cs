using JobScout.Core.Model;

namespace JobScout.Core.Abstractions;

/// <summary>
/// Persistence + history for tracked postings. Implementations own the per-posting timeline
/// (first/last seen, repost count) and recompute intent scores from that accumulated history.
/// The crawl orchestrator depends only on this contract.
/// </summary>
public interface IJobRepository
{
    /// <summary>
    /// Reconcile one feed's freshly-observed postings against what we already track: upsert the
    /// ones present, advance their timelines, mark previously-active postings that are now absent
    /// as closed, and rescore everything touched. Closure is scoped per (source, feed), so a feed
    /// only ever closes its own postings. Returns a summary of the deltas.
    /// </summary>
    Task<CrawlSummary> RecordCrawlAsync(
        JobSource source,
        string feed,
        IReadOnlyList<JobPosting> observed,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken);

    /// <summary>The highest-intent active postings currently tracked, best first.</summary>
    Task<IReadOnlyList<RankedPosting>> GetTopPostingsAsync(int limit, CancellationToken cancellationToken);
}
