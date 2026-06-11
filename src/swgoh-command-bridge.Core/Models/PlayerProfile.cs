#nullable enable

using System.Collections.Generic;

namespace swgoh_command_bridge.Core.Models
{
    /// <summary>
    /// Represents the player profile including characters and mods.
    /// </summary>
    public record PlayerProfile(
        string AllyCode,
        string Name,
        int Level,
        long GalacticPower,
        IReadOnlyCollection<Character> Characters,
        IReadOnlyCollection<GameMod> Mods
    );
}