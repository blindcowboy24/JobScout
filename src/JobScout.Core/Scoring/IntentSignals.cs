using JobScout.Core.Model;

namespace JobScout.Core.Scoring;

/// <summary>
/// The features fed to the scorer for one posting. These are derived by the data layer
/// from the posting plus its accumulated crawl history — the scorer itself stays pure and
/// has no idea where the numbers came from.
/// </summary>
/// <remarks>
/// Nullable fields encode genuine "unknown" (the source never told us), which the scorer
/// treats as neutral rather than guessing.
/// </remarks>
public sealed record IntentSignals
{
    public required JobSource Source { get; init; }

    /// <summary>Days between our first observation of this posting and <see cref="Today"/>.</summary>
    public required int DaysSinceFirstSeen { get; init; }

    /// <summary>Days since we last saw it in a feed. 0 means it was present in the latest crawl.</summary>
    public required int DaysSinceLastSeen { get; init; }

    /// <summary>How many times the posting disappeared from the feed and later reappeared.</summary>
    public required int RepostCount { get; init; }

    /// <summary>How many other tracked boards carry what looks like the same role right now.</summary>
    public int CrossPostCount { get; init; }

    public bool? HasSalaryBand { get; init; }
    public bool? HasScreeningQuestions { get; init; }
    public DateOnly? ApplicationDeadline { get; init; }

    /// <summary>The reference "now" the score is computed against (injected for testability).</summary>
    public required DateOnly Today { get; init; }
}
