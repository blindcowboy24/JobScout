using JobScout.Core.Scoring;
using JobScout.Data.Entities;

namespace JobScout.Data;

/// <summary>
/// Turns a stored <see cref="TrackedPosting"/> (plus its cross-post count and a reference date)
/// into the <see cref="IntentSignals"/> the scorer consumes. Shared by the write path
/// (rescoring during a crawl) and the read path (recomputing a breakdown for the detail view),
/// so the two can never drift apart.
/// </summary>
internal static class PostingScoring
{
    public static IntentSignals BuildSignals(TrackedPosting p, int crossPostCount, DateOnly today) => new()
    {
        Source = p.Source,
        DaysSinceFirstSeen = DayGap(p.FirstSeen, today),
        DaysSinceLastSeen = p.IsActive ? 0 : DayGap(p.LastSeen, today),
        RepostCount = p.RepostCount,
        CrossPostCount = Math.Max(0, crossPostCount),
        HasSalaryBand = p.HasSalaryBand,
        HasScreeningQuestions = p.HasScreeningQuestions,
        ApplicationDeadline = p.ApplicationDeadline,
        Today = today,
    };

    /// <summary>Lowercased, whitespace-collapsed title for loose cross-board matching.</summary>
    public static string NormalizeTitle(string title) =>
        string.Join(' ', title.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static DateOnly Today(DateTimeOffset reference) => DateOnly.FromDateTime(reference.UtcDateTime);

    private static int DayGap(DateTimeOffset from, DateOnly to) =>
        Math.Max(0, to.DayNumber - DateOnly.FromDateTime(from.UtcDateTime).DayNumber);
}
