#nullable enable

using System.Collections.Generic;
using System;

namespace swgoh_command_bridge.Core.Database.Entities
{
    /// <summary>
    /// Database representation of a player's cached profile details.
    /// </summary>
    public class PlayerEntity
    {
        public string AllyCode { get; set; } = string.Empty;
        
        public string Name { get; set; } = string.Empty;
        
        public int Level { get; set; }
        
        public long GalacticPower { get; set; }

        /// <summary>
        /// Gets or sets the UTC time when the live profile was last persisted.
        /// Null indicates a legacy cache created before sync freshness was tracked.
        /// </summary>
        public DateTime? LastSyncedUtc { get; set; }
        
        public virtual ICollection<CharacterEntity> Characters { get; set; } = new List<CharacterEntity>();
        
        public virtual ICollection<GameModEntity> Mods { get; set; } = new List<GameModEntity>();
    }
}
