using JobScout.Core.Abstractions;
using JobScout.Core.Model;
using JobScout.Core.Scoring;
using JobScout.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobScout.Data;

/// <summary>
/// EF Core implementation of <see cref="IJobRepository"/>. Owns the per-posting timeline and
/// drives rescoring: it reconciles a board's freshly-observed postings against the store,
/// advances first/last-seen and repost counts, marks vanished postings closed, then recomputes
/// intent scores from the resulting history.
/// </summary>
public sealed class JobRepository(JobScoutDbContext db, IIntentScorer scorer) : IJobRepository
{
    public async Task<CrawlSummary> RecordCrawlAsync(
        JobSource source,
        string feed,
        IReadOnlyList<JobPosting> observed,
        DateTimeOffset observedAt,
        CancellationToken ct)
    {
        var existing = await db.TrackedPostings
            .Where(p => p.Source == source && p.Feed == feed)
            .ToListAsync(ct);
        var byExternalId = existing.ToDictionary(p => p.ExternalId);

        var observedIds = new HashSet<string>(observed.Count);
        int newCount = 0, updatedCount = 0, reappearedCount = 0, closedCount = 0;
        var touched = new List<TrackedPosting>();

        foreach (var posting in observed)
        {
            observedIds.Add(posting.ExternalId);

            if (byExternalId.TryGetValue(posting.ExternalId, out var tracked))
            {
                var reappeared = !tracked.IsActive; // was gone last we checked, back now
                if (reappeared)
                {
                    tracked.RepostCount++;
                    reappearedCount++;
                }
                else
                {
                    updatedCount++;
                }

                var changed = tracked.ContentHash != posting.ContentHash;
                ApplyFields(tracked, posting);
                tracked.IsActive = true;
                tracked.LastSeen = observedAt;
                tracked.ObservationCount++;

                AddSnapshot(tracked, observedAt, present: true, changed);
                touched.Add(tracked);
            }
            else
            {
                tracked = NewTracked(source, feed, posting, observedAt);
                ApplyFields(tracked, posting);
                AddSnapshot(tracked, observedAt, present: true, changed: true);
                db.TrackedPostings.Add(tracked);
                newCount++;
                touched.Add(tracked);
            }
        }

        // Anything we tracked as active but didn't see this crawl has closed.
        foreach (var tracked in existing.Where(p => p.IsActive && !observedIds.Contains(p.ExternalId)))
        {
            tracked.IsActive = false;
            AddSnapshot(tracked, observedAt, present: false, changed: false);
            closedCount++;
            touched.Add(tracked);
        }

        // Persist timeline changes first so cross-post counting sees a consistent active set.
        await db.SaveChangesAsync(ct);

        await RescoreAsync(touched, observedAt, ct);
        await db.SaveChangesAsync(ct);

        return new CrawlSummary
        {
            Source = source,
            Feed = feed,
            New = newCount,
            Updated = updatedCount,
            Reappeared = reappearedCount,
            Closed = closedCount,
        };
    }

    public async Task<IReadOnlyList<RankedPosting>> GetTopPostingsAsync(int limit, CancellationToken ct)
    {
        // The active set is bounded (a handful of boards), and SQLite can't ORDER BY a
        // DateTimeOffset — so project the active rows and rank them client-side, which keeps
        // the tie-break (freshest first) working identically across providers.
        var active = await db.TrackedPostings
            .Where(p => p.IsActive)
            .Select(p => new RankedPosting
            {
                Source = p.Source,
                Company = p.Company,
                Title = p.Title,
                Location = p.Location,
                Url = p.Url,
                Score = p.Score,
                Band = p.Band,
                FirstSeen = p.FirstSeen,
                LastSeen = p.LastSeen,
                IsActive = p.IsActive,
            })
            .ToListAsync(ct);

        return active
            .OrderByDescending(p => p.Score)
            .ThenByDescending(p => p.LastSeen)
            .Take(limit)
            .ToList();
    }

    private async Task RescoreAsync(List<TrackedPosting> touched, DateTimeOffset observedAt, CancellationToken ct)
    {
        var today = PostingScoring.Today(observedAt);

        // Cross-post counts: how many active postings (anywhere) share a normalized title.
        var activeTitles = await db.TrackedPostings
            .Where(p => p.IsActive)
            .Select(p => p.Title)
            .ToListAsync(ct);
        var titleCounts = activeTitles
            .GroupBy(PostingScoring.NormalizeTitle)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var p in touched)
        {
            var sameTitle = p.IsActive && titleCounts.TryGetValue(PostingScoring.NormalizeTitle(p.Title), out var n) ? n - 1 : 0;

            var score = scorer.Score(PostingScoring.BuildSignals(p, sameTitle, today));
            p.Score = score.Value;
            p.Band = score.Band;
            p.ScoredAt = observedAt;

            // Stamp the score onto this crawl's snapshot so trends are reconstructable.
            var snap = p.Snapshots.LastOrDefault(s => s.ObservedAt == observedAt);
            if (snap is not null) snap.Score = score.Value;
        }
    }

    private static TrackedPosting NewTracked(JobSource source, string feed, JobPosting posting, DateTimeOffset observedAt) => new()
    {
        Source = source,
        Feed = feed,
        ExternalId = posting.ExternalId,
        FirstSeen = observedAt,
        LastSeen = observedAt,
        IsActive = true,
        ObservationCount = 1,
        RepostCount = 0,
    };

    private static void ApplyFields(TrackedPosting tracked, JobPosting posting)
    {
        tracked.Company = posting.Company;
        tracked.Title = posting.Title;
        tracked.Location = posting.Location;
        tracked.Department = posting.Department;
        tracked.Url = posting.Url;
        tracked.Remote = posting.Remote;
        tracked.Description = posting.Description;
        tracked.PostedAt = posting.PostedAt;
        tracked.SourceUpdatedAt = posting.UpdatedAt;
        tracked.ApplicationDeadline = posting.ApplicationDeadline;
        tracked.HasScreeningQuestions = posting.HasScreeningQuestions;
        tracked.ContentHash = posting.ContentHash;

        if (posting.Salary is { } salary)
        {
            tracked.HasSalaryBand = salary.IsDisclosed;
            tracked.SalaryMin = salary.Min;
            tracked.SalaryMax = salary.Max;
            tracked.SalaryCurrency = salary.Currency;
            tracked.SalaryInterval = salary.Interval;
        }
        else
        {
            tracked.HasSalaryBand = null; // unknown, not "no band" — the source stayed silent
        }
    }

    private static void AddSnapshot(TrackedPosting tracked, DateTimeOffset observedAt, bool present, bool changed) =>
        tracked.Snapshots.Add(new PostingSnapshot
        {
            ObservedAt = observedAt,
            WasPresent = present,
            ContentChanged = changed,
            ContentHash = tracked.ContentHash,
        });
}
