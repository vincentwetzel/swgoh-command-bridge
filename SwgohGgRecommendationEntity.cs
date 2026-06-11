#nullable enable

using System;

namespace swgoh_command_bridge.Core.Database.Entities
{
    /// <summary>
    /// Database entity representing the scraped swgoh.gg recommendations for a specific character.
    /// </summary>
    public class SwgohGgRecommendationEntity
    {
        public string CharacterId { get; set; } = string.Empty;

        public string RecommendedSets { get; set; } = string.Empty;

        public string ArrowPrimary { get; set; } = string.Empty;

        public string TrianglePrimary { get; set; } = string.Empty;

        public string CirclePrimary { get; set; } = string.Empty;

        public string CrossPrimary { get; set; } = string.Empty;

        public DateTime LastUpdatedUtc { get; set; }
    }
}