#nullable enable

using System.ComponentModel.DataAnnotations.Schema;

namespace swgoh_command_bridge.Core.Database.Entities
{
    /// <summary>
    /// Database representation of an equipped or inventory mod.
    /// </summary>
    public class GameModEntity
    {
        public string Id { get; set; } = string.Empty;
        
        public string PlayerAllyCode { get; set; } = string.Empty;
        
        public string CharacterId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name projected from the cached character roster for UI rendering.
        /// This value is not persisted because the character entity owns the canonical name.
        /// </summary>
        [NotMapped]
        public string OwnerDisplayName { get; set; } = string.Empty;

        /// <summary>Gets or sets the cached owner projection used by portrait-aware mod views.</summary>
        [NotMapped]
        public CharacterEntity? OwnerCharacter { get; set; }

        /// <summary>Gets or sets the compact quality summary projected for inventory rows.</summary>
        [NotMapped]
        public string QualitySummary { get; set; } = string.Empty;

        /// <summary>Gets or sets the readable set and slot summary projected for inventory rows.</summary>
        [NotMapped]
        public string SetSlotSummary { get; set; } = string.Empty;

        /// <summary>Gets or sets the readable primary-stat summary projected for inventory rows.</summary>
        [NotMapped]
        public string PrimaryStatSummary { get; set; } = string.Empty;

        /// <summary>Gets or sets the compact secondary-stat summary projected for inventory rows.</summary>
        [NotMapped]
        public string SecondaryStatsSummary { get; set; } = string.Empty;
        
        public int Set { get; set; }
        
        public int Slot { get; set; }
        
        public int Level { get; set; }
        
        public int Tier { get; set; }
        
        public int Rarity { get; set; }

        public string PrimaryStatType { get; set; } = "None";

        public double PrimaryStatValue { get; set; }

        public string SecondaryStatsJson { get; set; } = "[]";
        
        public virtual PlayerEntity Player { get; set; } = null!;
    }
}
