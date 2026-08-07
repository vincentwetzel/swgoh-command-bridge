#nullable enable

using System.Collections.Generic;
using swgoh_command_bridge.Core.Database.Entities;

namespace swgoh_command_bridge.Core.Models;

/// <summary>
/// Explains how one mod contributed to a calculated loadout.
/// </summary>
public sealed record ModAssignmentExplanation(
    string ModId,
    int Slot,
    double Score,
    string Reason);

/// <summary>
/// Describes a calculated loadout independently from the presentation layer.
/// </summary>
public sealed record ModLoadoutResult(
    IReadOnlyList<GameModEntity> Mods,
    bool HasRecommendation,
    bool IsComplete,
    bool MeetsSetRules,
    string Status,
    IReadOnlyList<ModAssignmentExplanation> Explanations)
{
    public IReadOnlyList<ModAssignmentAlternative> Alternatives { get; init; } =
        System.Array.Empty<ModAssignmentAlternative>();

    public IReadOnlyList<ModSwapPlan> SwapRecommendations { get; init; } =
        System.Array.Empty<ModSwapPlan>();
}
