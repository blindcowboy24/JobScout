using JobScout.Core.Model;

namespace JobScout.Core.Abstractions;

/// <summary>
/// A client for one job source. Implementations fetch the current set of public postings for a
/// given <em>feed</em> and map them onto the canonical <see cref="JobPosting"/>.
/// </summary>
public interface IAtsClient
{
    /// <summary>The source this client talks to. Used to route a configured feed to its client.</summary>
    JobSource Source { get; }

    /// <summary>
    /// Fetch the currently-listed postings for <paramref name="feed"/>. The feed is the source's
    /// crawl unit: for a company ATS it's the board slug in the URL; for an aggregator it's a
    /// search query or tag. Returns an empty list when the feed exists but yields no roles; throws
    /// on transport/parse failure so the caller can decide whether to skip this feed for the cycle.
    /// </summary>
    Task<IReadOnlyList<JobPosting>> FetchPostingsAsync(string feed, CancellationToken cancellationToken);
}
