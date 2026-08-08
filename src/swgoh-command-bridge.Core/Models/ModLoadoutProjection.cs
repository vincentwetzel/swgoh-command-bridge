#nullable enable

using System;
using System.Collections.Generic;

namespace swgoh_command_bridge.Core.Models;

/// <summary>One persisted mod-stat total before and after a proposed loadout.</summary>
public sealed record ModStatImpact(
    string StatName,
    double CurrentValue,
    double ProposedValue)
{
    public double Delta => ProposedValue - CurrentValue;

    public string Summary =>
        $"{StatName}: {CurrentValue:F2} -> {ProposedValue:F2} " +
        $"({Delta:+0.00;-0.00;0.00})";
}

/// <summary>
/// Compares only persisted mod-stat totals for a character's current and proposed loadouts.
/// </summary>
public sealed record ModLoadoutProjection(
    bool HasCurrentEquippedMods,
    IReadOnlyList<ModStatImpact> StatImpacts)
{
    public bool HasChanges => StatImpacts.Count > 0;

    public string Disclaimer =>
        "Projected mod-stat deltas exclude base character stats, set bonuses, and in-game stat conversion rules.";

    public static ModLoadoutProjection Empty { get; } =
        new(false, Array.Empty<ModStatImpact>());
}
