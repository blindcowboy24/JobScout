namespace JobScout.Core.Scoring;

/// <summary>One named contribution to a score, kept so the result can explain itself.</summary>
public sealed record ScoreFactor(string Name, double Delta, string Note);

/// <summary>
/// The output of the scorer: a single 0–100 intent value plus the factor-by-factor
/// breakdown that produced it. The breakdown is the point — this is a ranking aid a human
/// reviews, not an oracle, so it has to show its work.
/// </summary>
public sealed record IntentScore
{
    /// <summary>Final score clamped to 0–100. Higher = more likely a fresh, real, fillable role.</summary>
    public required double Value { get; init; }

    public required IReadOnlyList<ScoreFactor> Factors { get; init; }

    /// <summary>A coarse bucket for quick triage / sorting in a UI.</summary>
    public IntentBand Band => Value switch
    {
        >= 66 => IntentBand.High,
        >= 33 => IntentBand.Medium,
        _ => IntentBand.Low,
    };
}

public enum IntentBand
{
    Low,
    Medium,
    High,
}
