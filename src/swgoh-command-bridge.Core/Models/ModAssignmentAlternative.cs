#nullable enable

namespace swgoh_command_bridge.Core.Models;

/// <summary>Describes a lower-ranked candidate that could fill a loadout slot.</summary>
public sealed record ModAssignmentAlternative(
    string ModId,
    int Slot,
    double Score,
    string Reason)
{
    /// <summary>Difference from the selected mod in the same slot; this is an assignment-score delta, not a game-stat guarantee.</summary>
    public double ScoreDelta { get; init; }

    public string BenefitSummary => ScoreDelta switch
    {
        > 0 => $"Estimated assignment-score gain: +{ScoreDelta:F1}.",
        < 0 => $"Estimated assignment-score change: {ScoreDelta:F1}.",
        _ => "Estimated assignment score is unchanged."
    };
}

/// <summary>Describes a higher-scoring replacement candidate for an equipped mod.</summary>
public sealed record ModSwapPlan(
    string CurrentModId,
    string CandidateModId,
    int Slot,
    double ScoreGain,
    string Reason)
{
    public string BenefitSummary =>
        $"Estimated assignment-score gain: +{ScoreGain:F1}; validate set rules before applying.";
}
