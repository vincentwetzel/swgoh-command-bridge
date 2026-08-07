#nullable enable

using System.Collections.Generic;

namespace swgoh_command_bridge.Core.Models;

/// <summary>Describes record-level losses tolerated while parsing a Comlink payload.</summary>
public sealed record PlayerSyncDiagnostics(
    int RosterRecordsSeen,
    int RosterRecordsSkipped,
    int InventoryRecordsSeen,
    int InventoryRecordsSkipped,
    int DuplicateModsSkipped,
    IReadOnlyList<string> Warnings)
{
    public int EquippedModRecordsSeen { get; init; }

    public int EquippedModRecordsSkipped { get; init; }

    public bool HasWarnings => Warnings.Count > 0;

    public string Summary => HasWarnings
        ? string.Join(" ", Warnings)
        : "Payload parsed without record-level warnings.";
}
