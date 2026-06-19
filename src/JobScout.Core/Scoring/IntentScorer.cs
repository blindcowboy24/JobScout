using JobScout.Core.Model;

namespace JobScout.Core.Scoring;

/// <summary>
/// A transparent, additive intent scorer. It starts from a neutral baseline and applies
/// named factors that push the score up (signals of a genuine, budgeted, fillable req) or
/// down (signals of an evergreen / ghost / churned posting).
/// </summary>
/// <remarks>
/// The weights are deliberately simple constants rather than a learned model: this is a
/// portfolio project where the <em>reasoning</em> needs to be legible, and the time-based
/// signals only become meaningful after several crawls have accumulated history. Tune the
/// constants here in one place; the breakdown on <see cref="IntentScore"/> makes the effect
/// of any change easy to read off.
/// </remarks>
public sealed class IntentScorer : IIntentScorer
{
    // Neutral starting point. A brand-new posting from a direct ATS with no other signal
    // sits a little above the midpoint — innocent until the history says otherwise.
    private const double Baseline = 55;

    // A posting that lingers "open" for months without ever filling is the classic ghost tell.
    // Penalty ramps with age but saturates so a genuinely hard-to-fill senior role isn't buried.
    private const double StaleAgePenaltyPerWeek = 1.6;
    private const int StaleAgeGraceDays = 21;   // first three weeks are free — normal hiring latency
    private const double MaxStaleAgePenalty = 28;

    // Disappearing from the feed means it's likely filled/closed — not fillable right now.
    private const double GonePenaltyPerWeek = 6;
    private const double MaxGonePenalty = 30;

    // Repeatedly vanishing and reappearing is churn: evergreen reqs, pipeline-building, resellers.
    private const double RepostPenaltyEach = 5;
    private const double MaxRepostPenalty = 20;

    // Same role blasted across many boards at once reads as staffing churn rather than one real seat.
    private const double CrossPostPenaltyEach = 3;
    private const double MaxCrossPostPenalty = 15;

    private const double SalaryBandBonus = 10;          // disclosed pay → budgeted, legally-committed
    private const double ScreeningQuestionsBonus = 6;   // custom questions → real screening effort
    private const double FutureDeadlineBonus = 6;       // a live close date → an actual timeline
    private const double PastDeadlinePenalty = 12;      // still listed past its own deadline → ghost

    public IntentScore Score(IntentSignals s)
    {
        var factors = new List<ScoreFactor>
        {
            new("Baseline", Baseline, $"Direct ATS source ({s.Source})"),
        };

        AddStaleAge(s, factors);
        AddFreshness(s, factors);
        AddRepostChurn(s, factors);
        AddCrossPost(s, factors);
        AddSalary(s, factors);
        AddScreening(s, factors);
        AddDeadline(s, factors);

        var raw = factors.Sum(f => f.Delta);
        var value = Math.Clamp(raw, 0, 100);
        return new IntentScore { Value = Math.Round(value, 1), Factors = factors };
    }

    private static void AddStaleAge(IntentSignals s, List<ScoreFactor> factors)
    {
        var agedDays = s.DaysSinceFirstSeen - StaleAgeGraceDays;
        if (agedDays <= 0)
        {
            factors.Add(new("Posting age", 0, $"Within {StaleAgeGraceDays}-day grace ({s.DaysSinceFirstSeen}d old)"));
            return;
        }

        var penalty = Math.Min(agedDays / 7.0 * StaleAgePenaltyPerWeek, MaxStaleAgePenalty);
        factors.Add(new("Posting age", -penalty, $"Open {s.DaysSinceFirstSeen}d — lingering req"));
    }

    private static void AddFreshness(IntentSignals s, List<ScoreFactor> factors)
    {
        if (s.DaysSinceLastSeen <= 0)
        {
            factors.Add(new("Feed freshness", 0, "Present in latest crawl"));
            return;
        }

        var penalty = Math.Min(s.DaysSinceLastSeen / 7.0 * GonePenaltyPerWeek, MaxGonePenalty);
        factors.Add(new("Feed freshness", -penalty, $"Gone from feed {s.DaysSinceLastSeen}d — likely closed"));
    }

    private static void AddRepostChurn(IntentSignals s, List<ScoreFactor> factors)
    {
        if (s.RepostCount <= 0) return;
        var penalty = Math.Min(s.RepostCount * RepostPenaltyEach, MaxRepostPenalty);
        factors.Add(new("Repost churn", -penalty, $"Reappeared {s.RepostCount}× after vanishing"));
    }

    private static void AddCrossPost(IntentSignals s, List<ScoreFactor> factors)
    {
        if (s.CrossPostCount <= 0) return;
        var penalty = Math.Min(s.CrossPostCount * CrossPostPenaltyEach, MaxCrossPostPenalty);
        factors.Add(new("Cross-posting", -penalty, $"Same role on {s.CrossPostCount} other board(s)"));
    }

    private static void AddSalary(IntentSignals s, List<ScoreFactor> factors)
    {
        if (s.HasSalaryBand is null)
        {
            factors.Add(new("Salary band", 0, "Source does not expose pay"));
            return;
        }

        factors.Add(s.HasSalaryBand.Value
            ? new("Salary band", SalaryBandBonus, "Pay range disclosed")
            : new("Salary band", 0, "No pay range"));
    }

    private static void AddScreening(IntentSignals s, List<ScoreFactor> factors)
    {
        if (s.HasScreeningQuestions is null)
        {
            factors.Add(new("Screening questions", 0, "Not exposed by source"));
            return;
        }

        factors.Add(s.HasScreeningQuestions.Value
            ? new("Screening questions", ScreeningQuestionsBonus, "Custom screening present")
            : new("Screening questions", 0, "No custom screening"));
    }

    private static void AddDeadline(IntentSignals s, List<ScoreFactor> factors)
    {
        if (s.ApplicationDeadline is not { } deadline) return;

        if (deadline >= s.Today)
        {
            factors.Add(new("Application deadline", FutureDeadlineBonus, $"Closes {deadline:yyyy-MM-dd}"));
        }
        else
        {
            factors.Add(new("Application deadline", -PastDeadlinePenalty, $"Past deadline {deadline:yyyy-MM-dd}, still listed"));
        }
    }
}
