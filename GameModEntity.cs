#nullable enable

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
        
        public int Set { get; set; }
        
        public int Slot { get; set; }
        
        public int Level { get; set; }
        
        public int Tier { get; set; }
        
        public int Rarity { get; set; }
        
        public virtual PlayerEntity Player { get; set; } = null!;
    }
}