namespace swgoh_command_bridge.Core.Models;

public enum ModSlot
{
    Square = 1,
    Arrow = 2,
    Diamond = 3,
    Triangle = 4,
    Circle = 5,
    Cross = 6
}

public enum ModSet
{
    Health = 1,
    Offense = 2,
    Defense = 3,
    Speed = 4,
    CriticalChance = 5,
    CriticalDamage = 6,
    Potency = 7,
    Tenacity = 8
}

public enum StatType
{
    None = 0,
    Health = 1,
    Strength = 2,
    Agility = 3,
    Tactics = 4,
    Speed = 5,
    PhysicalDamage = 6,
    SpecialDamage = 7,
    Armor = 8,
    Resistance = 9,
    ArmorPenetration = 10,
    ResistancePenetration = 11,
    DodgeChance = 12,
    DeflectionChance = 13,
    PhysicalCriticalChance = 14,
    SpecialCriticalChance = 15,
    CriticalDamage = 16,
    Potency = 17,
    Tenacity = 18,
    HealthSteal = 19,
    Protection = 28,
    Accuracy = 48,
    CriticalAvoidance = 49,
    
    // Percentage variants
    HealthPercent = 55,
    ProtectionPercent = 56,
    OffensePercent = 57,
    DefensePercent = 58
}
