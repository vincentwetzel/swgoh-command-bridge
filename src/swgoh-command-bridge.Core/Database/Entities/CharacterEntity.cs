#nullable enable

using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Database.Entities
{
    /// <summary>
    /// Database representation of a player's unlocked character.
    /// </summary>
    public class CharacterEntity
    {
        public string Id { get; set; } = string.Empty;
        
        public string PlayerAllyCode { get; set; } = string.Empty;
        
        public string Name { get; set; } = string.Empty;

        public string PortraitAsset { get; set; } = string.Empty;

        public string Alignment { get; set; } = "Neutral";
        
        public int Level { get; set; }
        
        public int Stars { get; set; }
        
        public int GearLevel { get; set; }

        public int RelicTier { get; set; }
        
        public long GalacticPower { get; set; }
        
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets the tier-list grouping. Unranked is the default for units
        /// that have not been placed on the priority board.
        /// </summary>
        public PriorityTier PriorityTier { get; set; }

        /// <summary>
        /// Gets or sets the left-to-right rank inside <see cref="PriorityTier"/>.
        /// </summary>
        public int PriorityOrder { get; set; }
        
        public virtual PlayerEntity Player { get; set; } = null!;
    }
}
