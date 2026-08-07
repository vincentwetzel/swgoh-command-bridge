#nullable enable

namespace swgoh_command_bridge.Core.Models;

/// <summary>Describes a lower-ranked candidate that could fill a loadout slot.</summary>
public sealed record ModAssignmentAlternative(
    string ModId,
    int Slot,
    double Score,
    string Reason);

/// <summary>Describes a higher-scoring replacement candidate for an equipped mod.</summary>
public sealed record ModSwapPlan(
    string CurrentModId,
    string CandidateModId,
    int Slot,
    double ScoreGain,
    string Reason);
