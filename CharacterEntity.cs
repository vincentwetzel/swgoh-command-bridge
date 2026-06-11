#nullable enable

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
        
        public int Level { get; set; }
        
        public int Stars { get; set; }
        
        public int GearLevel { get; set; }
        
        public long GalacticPower { get; set; }
        
        public int Priority { get; set; }
        
        public virtual PlayerEntity Player { get; set; } = null!;
    }
}