using System.Collections.Generic;

namespace swgoh_command_bridge.Core.Models;

public record GameMod(
    string Id,
    int Level,
    int Pips,             // 1-6 Stars
    int Tier,             // 1-5 (E, D, C, B, A / Grey, Green, Blue, Purple, Gold)
    ModSlot Slot,
    ModSet Set,
    ModStat Primary,
    List<ModStat> Secondaries,
    string? EquippedUnitId // Null if sitting unequipped in inventory
)
{
    public bool IsEquipped => !string.IsNullOrEmpty(EquippedUnitId);

    /// <summary>
    /// Evaluates if this mod contains a specific secondary stat type.
    /// Useful for instant search/filtering lists in the UI.
    /// </summary>
    public bool HasSecondary(StatType type, out ModStat? match)
    {
        match = Secondaries.Find(s => s.Type == type);
        return match != null;
    }
}
