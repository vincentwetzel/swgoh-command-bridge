#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core
{
    /// <summary>
    /// Service responsible for calculating optimal mod assignments and generating swap recommendations.
    /// </summary>
    public class ModAssignmentService
    {
        private readonly ILogger<ModAssignmentService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModAssignmentService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public ModAssignmentService(ILogger<ModAssignmentService> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        /// <summary>
        /// Assigns the best available mods to characters based on priority, avoiding duplicate assignments
        /// and generating swap suggestions.
        /// </summary>
        /// <param name="characters">The list of characters to assign mods to.</param>
        /// <param name="mods">The pool of available mods.</param>
        /// <param name="recommendations">The scraped swgoh.gg recommendations, indexed by Character ID.</param>
        /// <returns>A list of planned mod assignments and swap suggestions.</returns>
        public List<ModAssignmentPlan> AssignMods(List<Character> characters, List<GameMod> mods, Dictionary<string, SwgohGgRecommendationEntity>? recommendations = null)
        {
            ArgumentNullException.ThrowIfNull(characters);
            ArgumentNullException.ThrowIfNull(mods);

            _logger.LogInformation("Starting mod assignment for {CharacterCount} characters using {ModCount} mods", characters.Count, mods.Count);

            // Sort characters by priority in descending order
            var sortedCharacters = characters.OrderByDescending(c => c.Priority).ToList();

            var assignedModIds = new HashSet<string>(mods.Count);
            var plans = new List<ModAssignmentPlan>(sortedCharacters.Count);

            // Group mods by Slot beforehand to avoid repeating scans over the entire pool
            var modsBySlot = new Dictionary<ModSlot, List<GameMod>>();
            foreach (var mod in mods)
            {
                if (!modsBySlot.TryGetValue(mod.Slot, out var slotList))
                {
                    slotList = new List<GameMod>();
                    modsBySlot[mod.Slot] = slotList;
                }
                slotList.Add(mod);
            }

            // Get all slots
            var slots = (ModSlot[])Enum.GetValues(typeof(ModSlot));

            foreach (var character in sortedCharacters)
            {
                var assignedModsForCharacter = new List<AssignedModDetail>(slots.Length);
                var swapRecommendations = new List<ModSwapRecommendation>();

                // Pre-deserialize community recommendations once per character to avoid JSON overhead in the hot loop
                List<RecommendedSet>? recommendedSets = null;
                Dictionary<string, List<RecommendedPrimary>>? recommendedPrimaries = null;

                if (recommendations != null && recommendations.TryGetValue(character.Id, out var rec))
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(rec.SetRecommendationsJson))
                        {
                            recommendedSets = JsonSerializer.Deserialize<List<RecommendedSet>>(rec.SetRecommendationsJson);
                        }
                        if (!string.IsNullOrWhiteSpace(rec.PrimaryStatsJson))
                        {
                            recommendedPrimaries = JsonSerializer.Deserialize<Dictionary<string, List<RecommendedPrimary>>>(rec.PrimaryStatsJson);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize swgoh.gg recommendations for character {CharacterId}", character.Id);
                    }
                }

                foreach (var slot in slots)
                {
                    if (!modsBySlot.TryGetValue(slot, out var availableModsInSlot))
                    {
                        continue;
                    }

                    GameMod? bestMod = null;
                    double bestScore = double.MinValue;

                    // Manual loop over available mods to avoid LINQ in hot path (Standard #18)
                    for (int i = 0; i < availableModsInSlot.Count; i++)
                    {
                        var mod = availableModsInSlot[i];
                        if (assignedModIds.Contains(mod.Id))
                        {
                            continue;
                        }

                        double score = ScoreMod(mod, recommendedSets, recommendedPrimaries);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestMod = mod;
                        }
                    }

                    if (bestMod != null)
                    {
                        assignedModIds.Add(bestMod.Id);

                        // Determine match details for selection explanation
                        bool setMatch = false;
                        if (recommendedSets != null)
                        {
                            string modSetString = bestMod.Set.ToString();
                            for (int k = 0; k < recommendedSets.Count; k++)
                            {
                                if (recommendedSets[k].Name.Equals(modSetString, StringComparison.OrdinalIgnoreCase))
                                {
                                    setMatch = true;
                                    break;
                                }
                            }
                        }

                        bool primaryMatch = false;
                        if (recommendedPrimaries != null)
                        {
                            string slotKey = bestMod.Slot switch
                            {
                                ModSlot.Arrow => "Arrow",
                                ModSlot.Triangle => "Triangle",
                                ModSlot.Circle => "Circle",
                                ModSlot.Cross => "Cross",
                                ModSlot.Square => "Square",
                                ModSlot.Diamond => "Diamond",
                                _ => string.Empty
                            };

                            if (!string.IsNullOrEmpty(slotKey) && recommendedPrimaries.TryGetValue(slotKey, out var recommendedList))
                            {
                                for (int k = 0; k < recommendedList.Count; k++)
                                {
                                    if (bestMod.Primary.Type.ToString().Contains(recommendedList[k].StatName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        primaryMatch = true;
                                        break;
                                    }
                                }
                            }
                        }

                        double speedVal = 0.0;
                        for (int k = 0; k < bestMod.Secondaries.Count; k++)
                        {
                            if (bestMod.Secondaries[k].Type.ToString().Equals("Speed", StringComparison.OrdinalIgnoreCase))
                            {
                                speedVal = bestMod.Secondaries[k].Value;
                                break;
                            }
                        }

                        string explanation = $"Base Score: {bestMod.Pips * 100 + bestMod.Tier * 20 + bestMod.Level}. " +
                                             $"Speed Secondary: +{speedVal} (Bonus: +{speedVal * 15.0}). " +
                                             $"Set Match: {(setMatch ? "Yes" : "No")}. " +
                                             $"Primary Match: {(primaryMatch ? "Yes" : "No")}.";

                        assignedModsForCharacter.Add(new AssignedModDetail(
                            bestMod,
                            setMatch,
                            primaryMatch,
                            bestScore,
                            explanation
                        ));

                        // If the mod is currently equipped on a different character, recommend a swap
                        if (bestMod.IsEquipped && bestMod.EquippedUnitId != character.Id)
                        {
                            // Try to find the name of the current owner
                            string sourceCharName = "Another Character";
                            for (int j = 0; j < characters.Count; j++)
                            {
                                if (characters[j].Id == bestMod.EquippedUnitId)
                                {
                                    sourceCharName = characters[j].Name;
                                    break;
                                }
                            }

                            swapRecommendations.Add(new ModSwapRecommendation(
                                bestMod,
                                bestMod.EquippedUnitId,
                                sourceCharName,
                                character.Id,
                                character.Name,
                                $"Higher priority character {character.Name} requires this high-quality mod."
                            ));
                        }
                    }
                }

                plans.Add(new ModAssignmentPlan(
                    character.Id,
                    character.Name,
                    assignedModsForCharacter,
                    swapRecommendations
                ));
            }

            // Calculate and log final Roster Coverage metrics
            int fullyModdedCount = 0;
            for (int i = 0; i < plans.Count; i++)
            {
                if (plans[i].AssignedMods.Count == 6)
                {
                    fullyModdedCount++;
                }
            }

            _logger.LogInformation("Roster coverage achieved: {FullyModdedCount}/{TotalCharacters} characters fully modded (6/6 slots filled).", fullyModdedCount, characters.Count);
            _logger.LogInformation("Successfully completed mod assignment planning. Generated {PlanCount} plans.", plans.Count);
            return plans;
        }

        /// <summary>
        /// Computes a quality score for a mod based on pips, tier, level, and secondary stats.
        /// </summary>
        private static double ScoreMod(
            GameMod mod,
            List<RecommendedSet>? recommendedSets,
            Dictionary<string, List<RecommendedPrimary>>? recommendedPrimaries)
        {
            // Base quality score
            double score = (mod.Pips * 100.0) + (mod.Tier * 20.0) + mod.Level;

            // Prioritize speed secondary if present
            for (int i = 0; i < mod.Secondaries.Count; i++)
            {
                var stat = mod.Secondaries[i];
                if (stat.Type.ToString().Equals("Speed", StringComparison.OrdinalIgnoreCase))
                {
                    score += stat.Value * 15.0;
                }
            }

            // Primary stat matching evaluation (Substantial boost for matching recommendations)
            if (recommendedPrimaries != null)
            {
                string slotKey = mod.Slot switch
                {
                    ModSlot.Arrow => "Arrow",
                    ModSlot.Triangle => "Triangle",
                    ModSlot.Circle => "Circle",
                    ModSlot.Cross => "Cross",
                    ModSlot.Square => "Square",
                    ModSlot.Diamond => "Diamond",
                    _ => string.Empty
                };

                if (!string.IsNullOrEmpty(slotKey) && recommendedPrimaries.TryGetValue(slotKey, out var recommendedList))
                {
                    for (int i = 0; i < recommendedList.Count; i++)
                    {
                        var recPrimary = recommendedList[i];
                        if (mod.Primary.Type.ToString().Contains(recPrimary.StatName, StringComparison.OrdinalIgnoreCase))
                        {
                            // Flexible matching: dynamic bonus scaled by usage popularity (maximum of 150 pts)
                            score += 1.5 * recPrimary.Percentage;
                            break;
                        }
                    }
                }
            }

            // Set matching evaluation (Moderate boost for matching recommended set sets)
            if (recommendedSets != null && recommendedSets.Count > 0)
            {
                string modSetString = mod.Set.ToString();
                for (int i = 0; i < recommendedSets.Count; i++)
                {
                    var recSet = recommendedSets[i];
                    if (recSet.Name.Equals(modSetString, StringComparison.OrdinalIgnoreCase))
                    {
                        // Dynamic set bonus matching scaled by usage popularity (maximum of 50 pts)
                        score += 0.5 * recSet.Percentage;
                        break;
                    }
                }
            }

            return score;
        }
    }
}

namespace swgoh_command_bridge.Core.Models
{
    /// <summary>
    /// Represents a recommended mod set alongside its usage percentage statistics.
    /// </summary>
    public record RecommendedSet(string Name, double Percentage);

    /// <summary>
    /// Represents a recommended primary stat alongside its usage percentage statistics.
    /// </summary>
    public record RecommendedPrimary(string StatName, double Percentage);
}
