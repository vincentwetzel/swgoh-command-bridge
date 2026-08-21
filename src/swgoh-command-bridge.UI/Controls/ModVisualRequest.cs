#nullable enable

using System;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.UI.Controls;

public sealed record ModVisualRequest(
    ModSlot Shape,
    ModSet Set,
    int Dots,
    int Level,
    int Tier = 5)
{
    public static ModVisualRequest FromEntity(GameModEntity mod)
    {
        ArgumentNullException.ThrowIfNull(mod);
        return new ModVisualRequest(
            (ModSlot)mod.Slot,
            (ModSet)mod.Set,
            mod.Rarity,
            mod.Level,
            mod.Tier);
    }
}
