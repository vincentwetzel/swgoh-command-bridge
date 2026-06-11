#nullable enable

using System;

namespace swgoh_command_bridge.Core.Database.Entities
{
    /// <summary>
    /// Database model representing swgoh.gg scraping insights for a character.
    /// </summary>
    public class SwgohGgRecommendationEntity
    {
        /// <summary>
        /// Gets or sets the unique character ID.
        /// </summary>
        public string CharacterId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets serialized JSON representing recommended primary stats by slot.
        /// </summary>
        public string PrimaryStatsJson { get; set; } = "{}";

        /// <summary>
        /// Gets or sets serialized JSON representing recommended mod sets.
        /// </summary>
        public string SetRecommendationsJson { get; set; } = "[]";

        /// <summary>
        /// Gets or sets the percentage weight/popularity of this recommendation.
        /// </summary>
        public double PopularityPercentage { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this recommendation data was last fetched.
        /// </summary>
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}