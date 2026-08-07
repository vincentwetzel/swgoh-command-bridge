#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services
{
    /// <summary>
    /// Matches inventory mods to cached swgoh.gg recommendations.
    /// </summary>
    public class ModAssignmentService : IModAssignmentService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ModAssignmentService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModAssignmentService"/> class.
        /// </summary>
        public ModAssignmentService(AppDbContext context, ILogger<ModAssignmentService> logger)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);

            _context = context;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyCollection<GameModEntity>> CalculateOptimalLoadoutAsync(
            string characterId,
            IEnumerable<GameModEntity> availableInventory,
            CancellationToken cancellationToken = default)
        {
            var result = await CalculateOptimalLoadoutResultAsync(
                characterId,
                availableInventory,
                cancellationToken).ConfigureAwait(false);
            return result.Mods;
        }

        /// <inheritdoc />
        public async Task<ModLoadoutResult> CalculateOptimalLoadoutResultAsync(
            string characterId,
            IEnumerable<GameModEntity> availableInventory,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(characterId);
            ArgumentNullException.ThrowIfNull(availableInventory);
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Calculating optimal mod loadout for character {CharacterId}", characterId);

            var recommendation = await _context.SwgohGgRecommendations
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.CharacterId == characterId, cancellationToken)
                .ConfigureAwait(false);

            var snapshot = recommendation == null
                ? null
                : RecommendationSnapshot.FromEntity(recommendation);
            var hasRecommendation = snapshot != null;
            var targetSets = snapshot == null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(
                    snapshot.Sets.Select(set => set.Name),
                    StringComparer.OrdinalIgnoreCase);
            var targetPrimaries = snapshot == null
                ? new Dictionary<int, IReadOnlyList<RecommendedPrimary>>()
                : snapshot.PrimaryStats
                    .Select(pair => new KeyValuePair<int, IReadOnlyList<RecommendedPrimary>>(
                        SlotNumber(pair.Key),
                        pair.Value))
                    .Where(pair => pair.Key > 0)
                    .GroupBy(pair => pair.Key)
                    .ToDictionary(
                        group => group.Key,
                        group => (IReadOnlyList<RecommendedPrimary>)group
                            .SelectMany(pair => pair.Value)
                            .ToList()
                            .AsReadOnly());

            var candidatesBySlot = availableInventory
                .GroupBy(m => m.Slot)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g
                    .OrderByDescending(m => ScoreMod(m, targetSets, targetPrimaries))
                    .ThenByDescending(m => m.Rarity)
                    .ThenByDescending(m => m.Level)
                    .ThenByDescending(m => m.Tier)
                    .ThenBy(m => m.Id, StringComparer.Ordinal)
                    .Take(8)
                    .ToList());

            var greedyLoadout = candidatesBySlot.Values
                .SelectMany(candidates => candidates.Take(1))
                .OrderBy(mod => mod.Slot)
                .Take(6)
                .ToList();

            if (greedyLoadout.Count == 0)
            {
                return BuildResult(
                    greedyLoadout,
                    hasRecommendation,
                    targetSets,
                    targetPrimaries,
                    "No inventory mods are available for this character.",
                    candidatesBySlot);
            }

            if (candidatesBySlot.Count < 6)
            {
                return BuildResult(
                    greedyLoadout,
                    hasRecommendation,
                    targetSets,
                    targetPrimaries,
                    $"Not enough inventory to fill all six mod slots ({candidatesBySlot.Count} of 6 available).",
                    candidatesBySlot);
            }

            var slots = Enum.GetValues<ModSlot>();
            var selected = new List<GameModEntity>(slots.Length);
            var usedModKeys = new HashSet<string>(StringComparer.Ordinal);
            var bestLoadout = greedyLoadout;
            var bestScore = double.MinValue;

            SearchValidLoadouts(
                0,
                0,
                slots,
                candidatesBySlot,
                targetSets,
                targetPrimaries,
                selected,
                usedModKeys,
                cancellationToken,
                ref bestScore,
                ref bestLoadout);

            var meetsSetRules = bestLoadout.Count == 6 && IsValidSetDistribution(bestLoadout);
            var status = meetsSetRules
                ? "Complete six-slot loadout satisfying set-bonus rules."
                : "Six slots are available, but no valid set-bonus distribution was found.";

            return BuildResult(
                bestLoadout,
                hasRecommendation,
                targetSets,
                targetPrimaries,
                status,
                candidatesBySlot);
        }

        public async Task<RosterLoadoutResult> CalculateRosterLoadoutsAsync(
            IEnumerable<CharacterEntity> characters,
            IEnumerable<GameModEntity> availableInventory,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(characters);
            ArgumentNullException.ThrowIfNull(availableInventory);
            cancellationToken.ThrowIfCancellationRequested();

            var orderedCharacters = characters
                .Where(character => !string.IsNullOrWhiteSpace(character.Id))
                .OrderByDescending(character => character.Priority)
                .ThenBy(character => character.Name, StringComparer.Ordinal)
                .ThenBy(character => character.Id, StringComparer.Ordinal)
                .ToList();
            var remainingInventory = availableInventory.ToList();
            var initialInventoryBySlot = remainingInventory
                .GroupBy(mod => mod.Slot)
                .ToDictionary(group => group.Key, group => group.Count());
            var plans = new List<RosterLoadoutPlan>(orderedCharacters.Count);
            var conflicts = new List<RosterLoadoutConflict>();

            foreach (var character in orderedCharacters)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await CalculateOptimalLoadoutResultAsync(
                    character.Id,
                    remainingInventory,
                    cancellationToken).ConfigureAwait(false);
                var assignedKeys = result.Mods
                    .Select(GetModKey)
                    .ToHashSet(StringComparer.Ordinal);
                remainingInventory.RemoveAll(mod => assignedKeys.Contains(GetModKey(mod)));

                var characterConflicts = new List<RosterLoadoutConflict>();
                foreach (var slot in Enum.GetValues<ModSlot>())
                {
                    if (result.Mods.Any(mod => mod.Slot == (int)slot))
                    {
                        continue;
                    }

                    var hasRemainingMod = remainingInventory.Any(mod => mod.Slot == (int)slot);
                    var reason = !initialInventoryBySlot.TryGetValue((int)slot, out var initialCount) ||
                                 initialCount == 0
                        ? $"No {slot} mod is available in the cached inventory."
                        : hasRemainingMod
                            ? $"A {slot} mod remains, but no valid complete loadout could use it with the set rules."
                            : $"No {slot} mod remained after higher-priority assignments reserved the available inventory.";

                    characterConflicts.Add(new RosterLoadoutConflict(
                        character.Id,
                        character.Name,
                        (int)slot,
                        reason));
                }

                conflicts.AddRange(characterConflicts);
                plans.Add(new RosterLoadoutPlan(
                    character.Id,
                    character.Name,
                    character.Priority,
                    result,
                    characterConflicts.AsReadOnly()));
            }

            var complete = plans.Count > 0 && plans.All(plan => plan.Loadout.IsComplete);
            var completeCount = plans.Count(plan => plan.Loadout.IsComplete);
            var status = plans.Count == 0
                ? "No characters are available for roster planning."
                : $"Planned {plans.Count} character(s) in priority-first order; {completeCount} complete loadout(s), {conflicts.Count} inventory conflict(s).";

            var assignedPlansByModKey = plans
                .SelectMany(plan => plan.Loadout.Mods.Select(mod => new
                {
                    ModKey = GetModKey(mod),
                    Plan = plan
                }))
                .GroupBy(item => item.ModKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Plan, StringComparer.Ordinal);
            var inventoryById = remainingInventory
                .Concat(plans.SelectMany(plan => plan.Loadout.Mods))
                .GroupBy(mod => mod.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var rosterSwaps = new List<RosterSwapRecommendation>();

            foreach (var plan in plans)
            {
                foreach (var swap in plan.Loadout.SwapRecommendations)
                {
                    var candidate = inventoryById.GetValueOrDefault(swap.CandidateModId);
                    var candidateOwner = candidate == null
                        ? null
                        : assignedPlansByModKey.GetValueOrDefault(GetModKey(candidate));
                    var isAvailable = candidate != null && candidateOwner == null;
                    var reason = candidate == null
                        ? "Candidate is no longer present in the cached inventory."
                        : candidateOwner == null
                            ? "Candidate is available in the unassigned roster inventory; verify set rules before applying."
                            : candidateOwner.CharacterId == plan.CharacterId
                                ? "Candidate is already assigned to this character; verify the slot-level swap manually."
                                : $"Candidate is currently reserved by {candidateOwner.CharacterName}; compare the priority trade-off before moving it.";

                    rosterSwaps.Add(new RosterSwapRecommendation(
                        plan.CharacterId,
                        plan.CharacterName,
                        plan.Priority,
                        swap.CurrentModId,
                        swap.CandidateModId,
                        swap.Slot,
                        swap.ScoreGain,
                        isAvailable,
                        reason));
                }
            }

            rosterSwaps = rosterSwaps
                .OrderByDescending(swap => swap.CandidateAvailable)
                .ThenByDescending(swap => swap.ScoreGain)
                .ThenByDescending(swap => swap.Priority)
                .ThenBy(swap => swap.CharacterId, StringComparer.Ordinal)
                .ThenBy(swap => swap.Slot)
                .ThenBy(swap => swap.CandidateModId, StringComparer.Ordinal)
                .Take(50)
                .ToList();

            return new RosterLoadoutResult(
                plans.AsReadOnly(),
                conflicts.AsReadOnly(),
                complete,
                status)
            {
                SwapRecommendations = rosterSwaps.AsReadOnly()
            };
        }

        private static void SearchValidLoadouts(
            int slotIndex,
            double currentScore,
            ModSlot[] slots,
            IReadOnlyDictionary<int, List<GameModEntity>> candidatesBySlot,
            IReadOnlySet<string> targetSets,
            IReadOnlyDictionary<int, IReadOnlyList<RecommendedPrimary>> targetPrimaries,
            List<GameModEntity> selected,
            HashSet<string> usedModKeys,
            CancellationToken cancellationToken,
            ref double bestScore,
            ref List<GameModEntity> bestLoadout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (slotIndex == slots.Length)
            {
                if (IsValidSetDistribution(selected) && currentScore > bestScore)
                {
                    bestScore = currentScore;
                    bestLoadout = selected.ToList();
                }

                return;
            }

            var slot = (int)slots[slotIndex];
            if (!candidatesBySlot.TryGetValue(slot, out var candidates))
            {
                return;
            }

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var modKey = $"{candidate.PlayerAllyCode}:{candidate.Id}";
                if (!usedModKeys.Add(modKey))
                {
                    continue;
                }

                selected.Add(candidate);
                SearchValidLoadouts(
                    slotIndex + 1,
                    currentScore + ScoreMod(candidate, targetSets, targetPrimaries),
                    slots,
                    candidatesBySlot,
                    targetSets,
                    targetPrimaries,
                    selected,
                    usedModKeys,
                    cancellationToken,
                    ref bestScore,
                    ref bestLoadout);
                selected.RemoveAt(selected.Count - 1);
                usedModKeys.Remove(modKey);
            }
        }

        private static bool IsValidSetDistribution(IReadOnlyCollection<GameModEntity> loadout)
        {
            if (loadout.Count != 6)
            {
                return false;
            }

            foreach (var setGroup in loadout.GroupBy(mod => (ModSet)mod.Set))
            {
                var requiredPieces = setGroup.Key is ModSet.Speed or ModSet.Offense or ModSet.CriticalDamage
                    ? 4
                    : 2;
                if (setGroup.Count() % requiredPieces != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static double ScoreMod(
            GameModEntity mod,
            IReadOnlySet<string> targetSets,
            IReadOnlyDictionary<int, IReadOnlyList<RecommendedPrimary>> targetPrimaries)
        {
            var score = (mod.Rarity * 10.0) + mod.Level + (mod.Tier * 2.0);

            if (targetSets.Contains(ModSetName(mod.Set)))
            {
                score += 30.0;
            }

            if (targetPrimaries.TryGetValue(mod.Slot, out var primaryRecommendations))
            {
                foreach (var recommendation in primaryRecommendations)
                {
                    if (mod.PrimaryStatType.Contains(recommendation.StatName, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 50.0 + recommendation.Percentage;
                        break;
                    }
                }
            }

            return score;
        }

        private static ModLoadoutResult BuildResult(
            IReadOnlyList<GameModEntity> loadout,
            bool hasRecommendation,
            IReadOnlySet<string> targetSets,
            IReadOnlyDictionary<int, IReadOnlyList<RecommendedPrimary>> targetPrimaries,
            string status,
            IReadOnlyDictionary<int, List<GameModEntity>>? candidatesBySlot = null)
        {
            var complete = loadout.Count == 6;
            var explanations = loadout
                .Select(mod => BuildExplanation(mod, targetSets, targetPrimaries))
                .ToList()
                .AsReadOnly();

            var result = new ModLoadoutResult(
                loadout.ToList().AsReadOnly(),
                hasRecommendation,
                complete,
                complete && IsValidSetDistribution(loadout),
                status,
                explanations);

            if (candidatesBySlot == null)
            {
                return result;
            }

            var chosenKeys = loadout.Select(GetModKey).ToHashSet(StringComparer.Ordinal);
            var alternatives = new List<ModAssignmentAlternative>();
            var swaps = new List<ModSwapPlan>();
            foreach (var selected in loadout.OrderBy(mod => mod.Slot).ThenBy(mod => mod.Id, StringComparer.Ordinal))
            {
                if (!candidatesBySlot.TryGetValue(selected.Slot, out var candidates))
                {
                    continue;
                }

                var selectedScore = ScoreMod(selected, targetSets, targetPrimaries);
                var alternativesForSlot = candidates
                    .Where(candidate => !chosenKeys.Contains(GetModKey(candidate)))
                    .Select(candidate => new
                    {
                        Mod = candidate,
                        Score = ScoreMod(candidate, targetSets, targetPrimaries)
                    })
                    .OrderByDescending(candidate => candidate.Score)
                    .ThenBy(candidate => candidate.Mod.Id, StringComparer.Ordinal)
                    .Take(2)
                    .ToList();

                foreach (var alternative in alternativesForSlot)
                {
                    var explanation = BuildExplanation(alternative.Mod, targetSets, targetPrimaries);
                    alternatives.Add(new ModAssignmentAlternative(
                        alternative.Mod.Id,
                        alternative.Mod.Slot,
                        alternative.Score,
                        explanation.Reason));

                    if (!string.IsNullOrWhiteSpace(selected.CharacterId) && alternative.Score > selectedScore)
                    {
                        swaps.Add(new ModSwapPlan(
                            selected.Id,
                            alternative.Mod.Id,
                            selected.Slot,
                            alternative.Score - selectedScore,
                            $"Candidate scores {alternative.Score - selectedScore:F1} higher; recheck set-bonus validity before swapping."));
                    }
                }
            }

            return result with
            {
                Alternatives = alternatives.AsReadOnly(),
                SwapRecommendations = swaps.AsReadOnly()
            };
        }

        private static ModAssignmentExplanation BuildExplanation(
            GameModEntity mod,
            IReadOnlySet<string> targetSets,
            IReadOnlyDictionary<int, IReadOnlyList<RecommendedPrimary>> targetPrimaries)
        {
            var score = ScoreMod(mod, targetSets, targetPrimaries);
            var reasons = new List<string>();

            if (targetSets.Contains(ModSetName(mod.Set)))
            {
                reasons.Add($"matches the recommended {ModSetName(mod.Set)} set");
            }

            if (targetPrimaries.TryGetValue(mod.Slot, out var primaryRecommendations))
            {
                var primary = primaryRecommendations.FirstOrDefault(recommendation =>
                    mod.PrimaryStatType.Contains(recommendation.StatName, StringComparison.OrdinalIgnoreCase));
                if (primary != null)
                {
                    reasons.Add($"matches the recommended {primary.StatName} primary");
                }
            }

            if (reasons.Count == 0)
            {
                reasons.Add("has the strongest available inventory score for this slot");
            }

            return new ModAssignmentExplanation(
                mod.Id,
                mod.Slot,
                score,
                $"{string.Join(" and ", reasons)} (score {score:F1}).");
        }

        private static int SlotNumber(string slotName)
        {
            var normalized = slotName.Trim();
            if (normalized.StartsWith("Slot ", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(normalized[5..], out var numericSlot))
            {
                return numericSlot is >= 1 and <= 6 ? numericSlot : 0;
            }

            return normalized.ToLowerInvariant() switch
            {
                "square" => (int)ModSlot.Square,
                "arrow" => (int)ModSlot.Arrow,
                "diamond" => (int)ModSlot.Diamond,
                "triangle" => (int)ModSlot.Triangle,
                "circle" => (int)ModSlot.Circle,
                "cross" => (int)ModSlot.Cross,
                _ => 0
            };
        }

        private static string ModSetName(int setId) => ((ModSet)setId).ToString();

        private static string GetModKey(GameModEntity mod) =>
            $"{mod.PlayerAllyCode}\u001F{mod.Id}";
    }
}
