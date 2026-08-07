# Mod Upgrade Mechanics

This document outlines the process of upgrading mods in *Star Wars: Galaxy of Heroes*. Understanding this flow is critical for the "Mod Advisor" feature.

## 1. Mod Levels

*   Mods can be upgraded from Level 1 to Level 15 using credits.
*   As a mod's level increases, its primary stat increases. The maximum value is reached at Level 15.
*   For mods with secondary stats, upgrading a mod to certain level thresholds will also upgrade one of the secondary stats. These thresholds are **Level 3, 6, 9, and 12**.

## 2. Mod Quality (Color / Tier) & Slicing

Mods have five quality tiers, represented by colors:
*   **Tier 1 (Grey):** No secondary stats revealed.
*   **Tier 2 (Green):** One secondary stat revealed.
*   **Tier 3 (Blue):** Two secondary stats revealed.
*   **Tier 4 (Purple):** Three secondary stats revealed.
*   **Tier 5 (Gold):** Four secondary stats revealed.

### Slicing Process

*   **Slicing** is the process of upgrading a mod's quality tier.
*   A mod must be **Level 15** to be sliced.
*   Slicing requires specific salvage materials (e.g., "Slicing Tech").
*   When a mod is sliced, one of two things happens:
    1.  **If the mod has fewer than four secondary stats:** A new secondary stat is revealed.
    2.  **If the mod already has four secondary stats:** One of the existing secondary stats is upgraded.
*   Slicing is only possible for mods that are **5 pips (stars)** or higher. 6-pip mods (6E to 6A) follow a similar, but more expensive, slicing path.

## 3. Stat Upgrades

*   When a mod is leveled up to 3, 6, 9, or 12, an existing secondary stat is upgraded.
*   The stat that gets upgraded is chosen randomly from the revealed secondary stats.
*   When a mod is sliced and already has four secondaries, one of them is randomly chosen to be upgraded.

## Implications for the Mod Advisor

The recommendation engine must understand this entire flow. For example:
*   When evaluating a **Green** mod at Level 1, the app considers the four level-based upgrades (at levels 3, 6, 9, and 12) plus a potential reveal when sliced to Blue.
*   The decision to "Upgrade", "Swap", or "Sell" a mod must take into account not just its current stats, but its *potential* stats after being fully leveled and sliced.
*   The user's thresholds should be applicable at different stages of this process (e.g., "a green mod at level 12 should have at least X speed to be worth slicing to blue").

## 4. Current advisor decision contract

The active advisor applies these decisions in order so the same inventory produces the same result:

1. **Sell** immediately when rarity is below the selected minimum. Rarity is a hard floor and takes precedence over level or speed potential.
2. **Level up** an eligible sub-level-15 mod when its calculated maximum Speed can reach the threshold. Sell it instead when required Speed cannot be reached.
3. **Slice** a level-15 mod that meets the Speed requirement but is below the selected minimum tier. A level-15 5-dot gold mod that otherwise meets the threshold is recommended for 6-dot slicing.
4. **Keep** a maxed mod when its rarity, tier, and required Speed all meet the threshold.
5. **Slice** a below-Speed-threshold mod only when it is below gold and the mechanics estimate says slicing can reach the required Speed.
6. **Swap** is considered only after upgrade/slicing options fail. The candidate must have the same set and primary-stat type as the target slot, must be faster than the equipped mod, and is never compared against the character currently equipping the candidate. Ties are resolved by character priority, then name, then ID.
7. **Sell** is the final result when no actionable upgrade, slice, keep, or compatible swap applies.

These are recommendations only; the application does not perform game actions or consume slicing materials.

The settings and transfer formats preserve an optional minimum-efficiency field for forward compatibility, but the current advisor contract does not use efficiency to change a recommendation. Speed, rarity/pips, tier, level, and compatible ownership are the active decision inputs.
