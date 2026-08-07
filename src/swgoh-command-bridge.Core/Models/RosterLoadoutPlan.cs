#nullable enable

using System.Collections.Generic;

namespace swgoh_command_bridge.Core.Models;

/// <summary>Describes an inventory collision encountered while planning a roster.</summary>
public sealed record RosterLoadoutConflict(
    string CharacterId,
    string CharacterName,
    int Slot,
    string Reason);

/// <summary>Contains one priority-ordered character's assigned loadout.</summary>
public sealed record RosterLoadoutPlan(
    string CharacterId,
    string CharacterName,
    int Priority,
    ModLoadoutResult Loadout,
    IReadOnlyList<RosterLoadoutConflict> Conflicts);

/// <summary>Describes a roster-level swap candidate and whether another plan currently reserves it.</summary>
public sealed record RosterSwapRecommendation(
    string CharacterId,
    string CharacterName,
    int Priority,
    string CurrentModId,
    string CandidateModId,
    int Slot,
    double ScoreGain,
    bool CandidateAvailable,
    string Reason);

/// <summary>Contains the deterministic, inventory-aware plan for multiple characters.</summary>
public sealed record RosterLoadoutResult(
    IReadOnlyList<RosterLoadoutPlan> Plans,
    IReadOnlyList<RosterLoadoutConflict> Conflicts,
    bool IsComplete,
    string Status)
{
    public IReadOnlyList<RosterSwapRecommendation> SwapRecommendations { get; init; } =
        System.Array.Empty<RosterSwapRecommendation>();
}
