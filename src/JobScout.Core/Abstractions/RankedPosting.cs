using JobScout.Core.Model;
using JobScout.Core.Scoring;

namespace JobScout.Core.Abstractions;

/// <summary>A tracked posting paired with its latest intent score, for ranked read-out.</summary>
public sealed record RankedPosting
{
    public required JobSource Source { get; init; }
    public required string Company { get; init; }
    public required string Title { get; init; }
    public string? Location { get; init; }
    public string? Url { get; init; }

    public required double Score { get; init; }
    public required IntentBand Band { get; init; }

    public required DateTimeOffset FirstSeen { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
    public required bool IsActive { get; init; }
}
