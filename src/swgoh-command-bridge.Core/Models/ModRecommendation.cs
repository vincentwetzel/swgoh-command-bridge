#nullable enable

namespace swgoh_command_bridge.Core.Models
{
    /// <summary>
    /// Represents a recommended action for a game mod.
    /// </summary>
    public enum ModRecommendationAction
    {
        /// <summary>
        /// Increase the level of the mod (1-15).
        /// </summary>
        LevelUp,

        /// <summary>
        /// Slice the mod to a higher tier (E -> D -> C -> B -> A).
        /// </summary>
        Slice,

        /// <summary>
        /// Equip the mod to a different character or upgrade its placement.
        /// </summary>
        Swap,

        /// <summary>
        /// Keep the mod as is.
        /// </summary>
        Keep,

        /// <summary>
        /// Sell the mod for credits.
        /// </summary>
        Sell
    }

    /// <summary>
    /// Domain record representing the recommendation output for a specific mod analysis.
    /// </summary>
    public record ModRecommendation(
        string ModId,
        ModRecommendationAction Action,
        string Reason,
        double Score
    );
}