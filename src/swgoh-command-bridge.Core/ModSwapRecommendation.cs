#nullable enable

namespace swgoh_command_bridge.Core.Models
{
    /// <summary>
    /// Represents a recommendation to swap a mod from a source location to a destination character.
    /// </summary>
    public record ModSwapRecommendation(
        GameMod Mod,
        string? SourceCharacterId,
        string? SourceCharacterName,
        string TargetCharacterId,
        string TargetCharacterName,
        string SwapReason
    );
}