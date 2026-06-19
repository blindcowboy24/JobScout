namespace JobScout.Core.Scoring;

/// <summary>Computes a posting-intent score from a set of derived signals.</summary>
public interface IIntentScorer
{
    IntentScore Score(IntentSignals signals);
}
