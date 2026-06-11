namespace swgoh_command_bridge.Core.Models;

public record ModStat(
    StatType Type,
    double Value,
    int RollCount = 1
)
{
    /// <summary>
    /// Formats the stat value for user interfaces (e.g., "+15 Speed" or "+5.88% Offense").
    /// </summary>
    public override string ToString()
    {
        bool isPercent = Type is StatType.HealthPercent 
                              or StatType.ProtectionPercent 
                              or StatType.OffensePercent 
                              or StatType.DefensePercent 
                              or StatType.Accuracy 
                              or StatType.CriticalAvoidance 
                              or StatType.CriticalDamage 
                              or StatType.Potency 
                              or StatType.Tenacity 
                              or StatType.PhysicalCriticalChance 
                              or StatType.SpecialCriticalChance;

        string formattedValue = isPercent ? $"{Value:F2}%" : $"{Value:F0}";
        string friendlyName = Type.ToString().Replace("Percent", "");
        
        return RollCount > 1 
            ? $"+{formattedValue} {friendlyName} ({RollCount})" 
            : $"+{formattedValue} {friendlyName}";
    }
}
