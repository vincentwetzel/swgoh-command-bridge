#nullable enable

using System;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services
{
    /// <summary>
    /// Service for evaluating SWGOH mod upgrade mechanics, predicting secondary rolls, and calculating upgrade potentials.
    /// </summary>
    public class ModMechanicsService
    {
        /// <summary>
        /// Calculates the potential maximum Speed secondary stat a mod can achieve after full level upgrades and slicing.
        /// </summary>
        /// <param name="mod">The game mod to evaluate.</param>
        /// <returns>The highest potential Speed secondary value this mod can reach.</returns>
        public double CalculatePotentialMaxSpeed(GameMod mod)
        {
            ArgumentNullException.ThrowIfNull(mod);

            double currentSpeed = 0;
            for (int i = 0; i < mod.Secondaries.Count; i++)
            {
                if (mod.Secondaries[i].Type == StatType.Speed)
                {
                    currentSpeed = mod.Secondaries[i].Value;
                    break;
                }
            }

            int remainingLevelRolls = GetRemainingLevelUpRolls(mod.Level);
            int remainingSlicingRolls = 5 - mod.Tier; // Max slice tier is 5 (Gold/A)
            double potentialSpeed = currentSpeed;

            if (currentSpeed > 0)
            {
                // Already has Speed: all remaining level/slice rolls could theoretically upgrade Speed
                potentialSpeed += (remainingLevelRolls + remainingSlicingRolls) * 6.0; // Standard max roll is 6
            }
            else
            {
                // Does not have speed yet. If it has less than 4 secondaries, it has a chance to reveal Speed.
                int currentSecondaryCount = mod.Secondaries.Count;
                if (currentSecondaryCount < 4)
                {
                    potentialSpeed += 6.0; // Best first roll is 6
                    int remainingSlots = 4 - currentSecondaryCount;
                    int rollsToUse = Math.Min(remainingSlots, remainingLevelRolls + remainingSlicingRolls);

                    // Upgrades remaining after revealing the Speed stat
                    int upgradesRemaining = (remainingLevelRolls + remainingSlicingRolls) - rollsToUse;
                    if (upgradesRemaining > 0)
                    {
                        potentialSpeed += upgradesRemaining * 6.0;
                    }
                }
            }

            // Slicing from 5-dot to 6-dot (Pips 5 to 6) adds a guaranteed flat +1 Speed
            if (mod.Pips == 5)
            {
                potentialSpeed += 1.0;
            }

            return potentialSpeed;
        }

        /// <summary>
        /// Determines how many secondary stat rolls are remaining during leveling.
        /// </summary>
        /// <param name="currentLevel">The current level of the mod.</param>
        /// <returns>The count of upgrade rolls remaining up to Level 12.</returns>
        public static int GetRemainingLevelUpRolls(int currentLevel)
        {
            int rolls = 0;
            if (currentLevel < 3) rolls++;
            if (currentLevel < 6) rolls++;
            if (currentLevel < 9) rolls++;
            if (currentLevel < 12) rolls++;
            return rolls;
        }
    }
}