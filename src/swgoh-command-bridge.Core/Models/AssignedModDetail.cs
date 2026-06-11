#nullable enable

namespace swgoh_command_bridge.Core.Models
{
    /// <summary>
    /// Details why an assigned mod was selected for a character.
    /// </summary>
    public record AssignedModDetail(
        GameMod Mod,
        bool SetMatch,
        bool PrimaryMatch,
        double Score,
        string Explanation
    );
}