using JobScout.Core.Abstractions;
using JobScout.Core.Model;
using JobScout.Core.Scoring;
using JobScout.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobScout.Data;

/// <summary>
/// EF Core implementation of the read-side queries. Filtering and counting run in SQL; ordering
/// and paging happen in memory over the bounded result set, because SQLite cannot ORDER BY a
/// <see cref="DateTimeOffset"/> — keeping the behaviour identical across SQLite and SQL Server.
/// The detail view's score breakdown is recomputed live from stored fields via the shared
/// <see cref="PostingScoring"/> logic, so it always matches how the crawl scored the posting.
/// </summary>
public sealed class PostingReadStore(JobScoutDbContext db, IIntentScorer scorer) : IPostingReadStore
{
    public async Task<IReadOnlyList<PostingListItem>> QueryAsync(PostingFilter filter, CancellationToken ct)
    {
        var rows = await MaterializeAsync(filter, ct);

        var ordered = Sort(rows, filter.Sort);

        // When searching, float the most relevant matches up: a term in the title beats one only
        // in the description (e.g. "Senior Engineer (.NET)" over a Datadog role that merely
        // mentions monitoring .NET apps). OrderBy is stable, so the chosen sort breaks ties.
        var groups = SearchQuery.Parse(filter.Search);
        if (groups.Count > 0)
            ordered = ordered.OrderByDescending(r => Relevance(r, groups));

        return ordered
            .Skip(Math.Max(0, filter.Skip))
            .Take(Math.Clamp(filter.Take, 1, 500))
            .ToList();
    }

    public async Task<int> CountAsync(PostingFilter filter, CancellationToken ct) =>
        (await MaterializeAsync(filter, ct)).Count;

    // SQL-safe filters run in the database; the age filter (relative to "now", over a DateTimeOffset
    // SQLite won't compute on) is applied in memory over the bounded result set. Both Query and Count
    // go through here so their results always agree.
    private async Task<List<PostingListItem>> MaterializeAsync(PostingFilter filter, CancellationToken ct)
    {
        var rows = await Filtered(filter).Select(Projection).ToListAsync(ct);

        if (filter.MaxAgeDays is { } maxAge)
        {
            var now = DateTimeOffset.UtcNow;
            rows = rows.Where(r => (now - r.FirstSeen).TotalDays <= maxAge).ToList();
        }

        return rows;
    }

    public async Task<PostingDetail?> GetDetailAsync(int id, CancellationToken ct)
    {
        var p = await db.TrackedPostings
            .Include(x => x.Snapshots)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;

        // Recompute the breakdown the same way the crawl would, including a fresh cross-post count.
        var crossPost = await CrossPostCountAsync(p, ct);
        var today = PostingScoring.Today(DateTimeOffset.UtcNow);
        var score = scorer.Score(PostingScoring.BuildSignals(p, crossPost, today));

        var timeline = p.Snapshots
            .OrderBy(s => s.ObservedAt)
            .TakeLast(30)
            .Select(s => new SnapshotPoint(s.ObservedAt, s.WasPresent, s.Score))
            .ToList();

        return new PostingDetail
        {
            Posting = ToListItem(p, score),
            Description = p.Description,
            SalarySummary = DescribeSalary(p),
            ApplicationDeadline = p.ApplicationDeadline,
            ObservationCount = p.ObservationCount,
            ScoredAt = p.ScoredAt,
            Factors = score.Factors,
            Timeline = timeline,
        };
    }

    public async Task<StoreStats> GetStatsAsync(CancellationToken ct)
    {
        var totalTracked = await db.TrackedPostings.CountAsync(ct);
        var totalActive = await db.TrackedPostings.CountAsync(p => p.IsActive, ct);

        var bySource = await db.TrackedPostings
            .Where(p => p.IsActive)
            .GroupBy(p => p.Source)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byBand = await db.TrackedPostings
            .Where(p => p.IsActive)
            .GroupBy(p => p.Band)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // MAX over a DateTimeOffset doesn't translate on SQLite, so reduce in memory.
        var lastSeenValues = await db.TrackedPostings.Select(p => p.LastSeen).ToListAsync(ct);
        DateTimeOffset? lastCrawl = lastSeenValues.Count > 0 ? lastSeenValues.Max() : null;

        return new StoreStats
        {
            TotalTracked = totalTracked,
            TotalActive = totalActive,
            ActiveBySource = bySource.ToDictionary(x => x.Key, x => x.Count),
            ActiveByBand = byBand.ToDictionary(x => x.Key, x => x.Count),
            LastCrawl = lastCrawl,
        };
    }

    private IQueryable<TrackedPosting> Filtered(PostingFilter filter)
    {
        var q = db.TrackedPostings.AsQueryable();

        if (filter.ActiveOnly) q = q.Where(p => p.IsActive);
        if (filter.Source is { } source) q = q.Where(p => p.Source == source);
        if (filter.Band is { } band) q = q.Where(p => p.Band == band);

        if (filter.RemoteOnly) q = q.Where(p => p.Remote == RemoteMode.Remote);
        if (filter.MinScore is { } minScore) q = q.Where(p => p.Score >= minScore);
        if (filter.MaxReposts is { } maxReposts) q = q.Where(p => p.RepostCount <= maxReposts);
        if (filter.HasSalary is { } hasSalary)
            q = hasSalary ? q.Where(p => p.HasSalaryBand == true) : q.Where(p => p.HasSalaryBand != true);

        if (!string.IsNullOrWhiteSpace(filter.TitleContains))
        {
            var t = filter.TitleContains.Trim().ToLower();
            q = q.Where(p => p.Title.ToLower().Contains(t));
        }
        if (!string.IsNullOrWhiteSpace(filter.CompanyContains))
        {
            var c = filter.CompanyContains.Trim().ToLower();
            q = q.Where(p => p.Company.ToLower().Contains(c));
        }
        if (!string.IsNullOrWhiteSpace(filter.LocationContains))
        {
            var l = filter.LocationContains.Trim().ToLower();
            q = q.Where(p => p.Location != null && p.Location.ToLower().Contains(l));
        }

        if (SearchQuery.Build(filter.Search) is { } predicate)
            q = q.Where(predicate);

        return q;
    }

    private async Task<int> CrossPostCountAsync(TrackedPosting p, CancellationToken ct)
    {
        if (!p.IsActive) return 0;
        var key = PostingScoring.NormalizeTitle(p.Title);
        var activeTitles = await db.TrackedPostings
            .Where(x => x.IsActive)
            .Select(x => x.Title)
            .ToListAsync(ct);
        return Math.Max(0, activeTitles.Count(t => PostingScoring.NormalizeTitle(t) == key) - 1);
    }

    // Higher = more relevant: title hit (3) > company hit (2) > matched only via location/description (1).
    private static int Relevance(PostingListItem p, IReadOnlyList<string[]> groups)
    {
        if (SearchQuery.Matches(p.Title, groups)) return 3;
        if (SearchQuery.Matches(p.Company, groups)) return 2;
        return 1;
    }

    private static IEnumerable<PostingListItem> Sort(IEnumerable<PostingListItem> items, PostingSort sort) => sort switch
    {
        PostingSort.ScoreAscending => items.OrderBy(p => p.Score).ThenByDescending(p => p.LastSeen),
        PostingSort.NewestFirst => items.OrderByDescending(p => p.FirstSeen),
        PostingSort.RecentlySeen => items.OrderByDescending(p => p.LastSeen),
        PostingSort.TitleAscending => items.OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase),
        _ => items.OrderByDescending(p => p.Score).ThenByDescending(p => p.LastSeen),
    };

    // Projects in SQL; Score/Band are read straight from the stored columns for list speed.
    private static readonly System.Linq.Expressions.Expression<Func<TrackedPosting, PostingListItem>> Projection =
        p => new PostingListItem
        {
            Id = p.Id,
            Source = p.Source,
            Company = p.Company,
            Title = p.Title,
            Location = p.Location,
            Department = p.Department,
            Url = p.Url,
            Remote = p.Remote,
            Score = p.Score,
            Band = p.Band,
            IsActive = p.IsActive,
            FirstSeen = p.FirstSeen,
            LastSeen = p.LastSeen,
            RepostCount = p.RepostCount,
            HasSalaryBand = p.HasSalaryBand,
        };

    private static PostingListItem ToListItem(TrackedPosting p, IntentScore score) => new()
    {
        Id = p.Id,
        Source = p.Source,
        Company = p.Company,
        Title = p.Title,
        Location = p.Location,
        Department = p.Department,
        Url = p.Url,
        Remote = p.Remote,
        Score = score.Value,
        Band = score.Band,
        IsActive = p.IsActive,
        FirstSeen = p.FirstSeen,
        LastSeen = p.LastSeen,
        RepostCount = p.RepostCount,
        HasSalaryBand = p.HasSalaryBand,
    };

    private static string? DescribeSalary(TrackedPosting p)
    {
        if (p.HasSalaryBand is not true) return null;
        if (p.SalaryMin is { } min && p.SalaryMax is { } max)
            return $"{Money(min, p.SalaryCurrency)}–{Money(max, p.SalaryCurrency)}{Per(p.SalaryInterval)}";
        if (p.SalaryMin is { } only) return $"{Money(only, p.SalaryCurrency)}+{Per(p.SalaryInterval)}";
        return "Disclosed"; // summary-only sources (e.g. Ashby) — band present, no parseable numbers
    }

    private static string Money(decimal amount, string? currency) =>
        $"{(string.IsNullOrWhiteSpace(currency) ? "" : currency + " ")}{amount:#,0}";

    private static string Per(string? interval) =>
        string.IsNullOrWhiteSpace(interval) ? "" : $" / {interval}";
}
