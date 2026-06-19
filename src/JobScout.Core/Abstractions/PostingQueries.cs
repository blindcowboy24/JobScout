using JobScout.Core.Model;
using JobScout.Core.Scoring;

namespace JobScout.Core.Abstractions;

/// <summary>How to sort a postings query. The defaults reflect the product thesis: best intent first.</summary>
public enum PostingSort
{
    ScoreDescending,
    ScoreAscending,
    NewestFirst,     // most recently first-seen
    RecentlySeen,    // most recently present in a feed
    TitleAscending,
}

/// <summary>The filter/sort/paging criteria for a postings list query. All filters are optional.</summary>
public sealed record PostingFilter
{
    public JobSource? Source { get; init; }
    public IntentBand? Band { get; init; }

    /// <summary>Broad relevance search over title / company / location / description (with ranking).</summary>
    public string? Search { get; init; }

    // --- Per-column filters (precise; combine with AND) ---

    public string? TitleContains { get; init; }
    public string? CompanyContains { get; init; }
    public string? LocationContains { get; init; }

    /// <summary>Keep only postings scoring at least this much.</summary>
    public double? MinScore { get; init; }

    /// <summary>Keep only postings first seen within this many days (freshness ceiling).</summary>
    public int? MaxAgeDays { get; init; }

    /// <summary>Keep only postings reposted at most this many times (churn ceiling).</summary>
    public int? MaxReposts { get; init; }

    /// <summary>true = only postings with a disclosed salary; false = only those without; null = either.</summary>
    public bool? HasSalary { get; init; }

    /// <summary>When true, keep only postings known to be remote.</summary>
    public bool RemoteOnly { get; init; }

    /// <summary>When true (default), only postings still present in the latest crawl are returned.</summary>
    public bool ActiveOnly { get; init; } = true;

    public PostingSort Sort { get; init; } = PostingSort.ScoreDescending;

    public int Skip { get; init; }
    public int Take { get; init; } = 50;
}

/// <summary>A row in the ranked postings list.</summary>
public sealed record PostingListItem
{
    public required int Id { get; init; }
    public required JobSource Source { get; init; }
    public required string Company { get; init; }
    public required string Title { get; init; }
    public string? Location { get; init; }
    public string? Department { get; init; }
    public string? Url { get; init; }
    public RemoteMode Remote { get; init; }

    public required double Score { get; init; }
    public required IntentBand Band { get; init; }

    public required bool IsActive { get; init; }
    public required DateTimeOffset FirstSeen { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
    public required int RepostCount { get; init; }
    public bool? HasSalaryBand { get; init; }
}

/// <summary>One observation in a posting's timeline, for the detail view's history.</summary>
public sealed record SnapshotPoint(DateTimeOffset ObservedAt, bool WasPresent, double Score);

/// <summary>Everything the detail view shows for one posting, including a freshly-computed score breakdown.</summary>
public sealed record PostingDetail
{
    public required PostingListItem Posting { get; init; }

    /// <summary>Plain-text description snippet (bounded) — what the search matches against.</summary>
    public string? Description { get; init; }

    public string? SalarySummary { get; init; }
    public DateOnly? ApplicationDeadline { get; init; }
    public required int ObservationCount { get; init; }
    public DateTimeOffset? ScoredAt { get; init; }

    /// <summary>The named factors that add up to the current score — the "why" behind the number.</summary>
    public required IReadOnlyList<ScoreFactor> Factors { get; init; }

    /// <summary>Recent observations, oldest→newest, for a small history view.</summary>
    public required IReadOnlyList<SnapshotPoint> Timeline { get; init; }
}

/// <summary>Headline counts for the dashboard header.</summary>
public sealed record StoreStats
{
    public required int TotalTracked { get; init; }
    public required int TotalActive { get; init; }
    public required IReadOnlyDictionary<JobSource, int> ActiveBySource { get; init; }
    public required IReadOnlyDictionary<IntentBand, int> ActiveByBand { get; init; }
    public DateTimeOffset? LastCrawl { get; init; }
}

/// <summary>
/// Read-side queries over the tracked-postings store. Deliberately separate from
/// <see cref="IJobRepository"/> (the crawl/write path): the UI only reads, so it depends only
/// on this. A small CQRS-style split that keeps each surface focused.
/// </summary>
public interface IPostingReadStore
{
    Task<IReadOnlyList<PostingListItem>> QueryAsync(PostingFilter filter, CancellationToken cancellationToken);
    Task<int> CountAsync(PostingFilter filter, CancellationToken cancellationToken);
    Task<PostingDetail?> GetDetailAsync(int id, CancellationToken cancellationToken);
    Task<StoreStats> GetStatsAsync(CancellationToken cancellationToken);
}
