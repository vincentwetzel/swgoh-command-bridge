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
    )
    {
        /// <summary>Estimated current efficiency from persisted secondary roll counts.</summary>
        public double CurrentEfficiency { get; init; }

        /// <summary>Estimated efficiency if remaining level/tier rolls improve the mod.</summary>
        public double ProjectedEfficiency { get; init; }

        /// <summary>Explains the estimate without implying guaranteed game-stat gains.</summary>
        public string EfficiencySummary =>
            $"Estimated secondary-roll efficiency: {CurrentEfficiency:F0}% current; {ProjectedEfficiency:F0}% projected maximum.";
    }
}
