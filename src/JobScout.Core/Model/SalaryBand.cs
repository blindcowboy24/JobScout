namespace JobScout.Core.Model;

/// <summary>
/// A disclosed pay range. Presence of a real band is one of the cheaper-to-verify
/// signals that a posting is a genuine, budgeted req rather than a placeholder.
/// </summary>
/// <remarks>
/// Some sources expose structured numbers (Lever's <c>salaryRange</c>); others only a
/// human-readable summary (Ashby). Either counts as "disclosed" for intent purposes.
/// </remarks>
public sealed record SalaryBand(
    decimal? Min,
    decimal? Max,
    string? Currency,
    string? Interval,
    string? Summary = null)
{
    /// <summary>True when the employer disclosed pay in any form — a number or a summary.</summary>
    public bool IsDisclosed => Min is > 0 || Max is > 0 || !string.IsNullOrWhiteSpace(Summary);
}
