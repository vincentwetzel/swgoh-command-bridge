#nullable enable

using System.Collections.Generic;

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
        
        public virtual ICollection<CharacterEntity> Characters { get; set; } = new List<CharacterEntity>();
        
        public virtual ICollection<GameModEntity> Mods { get; set; } = new List<GameModEntity>();
    }
}