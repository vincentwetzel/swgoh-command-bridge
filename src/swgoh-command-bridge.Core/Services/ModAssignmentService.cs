#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
        private const int GlobalOptimizationCharacterLimit = 12;
        private const int GlobalOptimizationCandidateLimit = 6;
        private const int GlobalOptimizationStateLimit = 100_000;
        private const int SecondaryScoreCacheLimit = 2_048;
        private static readonly ConcurrentDictionary<string, double> SecondaryScoreCache = new(StringComparer.Ordinal);
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

            var inventory = availableInventory.ToList();
            var currentEquippedMods = await LoadCurrentEquippedModsAsync(
                characterId,
                inventory,
                cancellationToken).ConfigureAwait(false);
            var scopedAllyCode = GetScopedAllyCode(inventory);

            _logger.LogInformation("Calculating optimal mod loadout for character {CharacterId}", characterId);

            var recommendation = await _context.SwgohGgRecommendations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    r => r.CharacterId == characterId && r.PlayerAllyCode == scopedAllyCode,
                    cancellationToken)
                .ConfigureAwait(false);

            var snapshot = recommendation == null
                ? null
                : RecommendationSnapshot.FromEntity(recommendation);
            var hasRecommendation = snapshot != null;
            var targetSets = snapshot == null
                ? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                : snapshot.Sets
                    .GroupBy(set => set.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Max(set => Math.Clamp(set.Percentage, 0, 100)),
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

            var candidatesBySlot = inventory
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
                    candidatesBySlot,
                    currentEquippedMods);
            }

            if (candidatesBySlot.Count < 6)
            {
                return BuildResult(
                    greedyLoadout,
                    hasRecommendation,
                    targetSets,
                    targetPrimaries,
                    $"Not enough inventory to fill all six mod slots ({candidatesBySlot.Count} of 6 available).",
                    candidatesBySlot,
                    currentEquippedMods);
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
                candidatesBySlot,
                currentEquippedMods);
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

        public async Task<RosterLoadoutResult> CalculateGloballyOptimizedRosterLoadoutsAsync(
            IEnumerable<CharacterEntity> characters,
            IEnumerable<GameModEntity> availableInventory,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(characters);
            ArgumentNullException.ThrowIfNull(availableInventory);
            cancellationToken.ThrowIfCancellationRequested();

            var orderedCharacters = OrderRosterCharacters(characters);
            if (orderedCharacters.Count > GlobalOptimizationCharacterLimit)
            {
                var boundedFallback = await CalculateRosterLoadoutsAsync(
                    orderedCharacters,
                    availableInventory,
                    cancellationToken).ConfigureAwait(false);
                return boundedFallback with
                {
                    Status = boundedFallback.Status +
                        $" Bounded global optimization is limited to {GlobalOptimizationCharacterLimit} characters; priority-first planning was used."
                };
            }

            var inventory = availableInventory.ToList();
            var groups = new List<RosterCandidateGroup>(orderedCharacters.Count);
            var priorityRemainingInventory = inventory.ToList();
            foreach (var character in orderedCharacters)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var independentResult = await CalculateOptimalLoadoutResultAsync(
                    character.Id,
                    inventory,
                    cancellationToken).ConfigureAwait(false);
                var priorityResult = await CalculateOptimalLoadoutResultAsync(
                    character.Id,
                    priorityRemainingInventory,
                    cancellationToken).ConfigureAwait(false);
                var priorityKeys = priorityResult.Mods.Select(GetModKey).ToHashSet(StringComparer.Ordinal);
                priorityRemainingInventory.RemoveAll(mod => priorityKeys.Contains(GetModKey(mod)));
                groups.Add(new RosterCandidateGroup(
                    character,
                    BuildRosterCandidates(character, inventory, independentResult, priorityResult)));
            }

            var selected = SelectGlobalRosterCandidates(groups, cancellationToken);
            var selectedByCharacterId = selected.ToDictionary(
                candidate => candidate.Character.Id,
                candidate => candidate.Loadout,
                StringComparer.Ordinal);
            var result = BuildRosterResult(
                orderedCharacters,
                inventory,
                selectedByCharacterId,
                "Bounded global roster optimization selected the best deterministic non-overlapping plan found.");

            return result with
            {
                Status = result.Status +
                    $" Evaluated up to {GlobalOptimizationStateLimit:N0} assignment states."
            };
        }

        private static List<CharacterEntity> OrderRosterCharacters(IEnumerable<CharacterEntity> characters) => characters
            .Where(character => !string.IsNullOrWhiteSpace(character.Id))
            .OrderByDescending(character => character.Priority)
            .ThenBy(character => character.Name, StringComparer.Ordinal)
            .ThenBy(character => character.Id, StringComparer.Ordinal)
            .ToList();

        private static IReadOnlyList<RosterCandidate> BuildRosterCandidates(
            CharacterEntity character,
            IReadOnlyList<GameModEntity> inventory,
            params ModLoadoutResult[] results)
        {
            var candidates = new List<RosterCandidate>();
            foreach (var result in results)
            {
                AddRosterCandidate(candidates, character, result);

                foreach (var alternative in result.Alternatives)
                {
                    var replacement = inventory.FirstOrDefault(mod =>
                        string.Equals(mod.Id, alternative.ModId, StringComparison.Ordinal) &&
                        mod.Slot == alternative.Slot);
                    if (replacement == null)
                    {
                        continue;
                    }

                    var replacementLoadout = result.Mods
                        .Where(mod => mod.Slot != alternative.Slot)
                        .Append(replacement)
                        .OrderBy(mod => mod.Slot)
                        .ThenBy(mod => mod.Id, StringComparer.Ordinal)
                        .ToList();
                    if (replacementLoadout.Count != 6 ||
                        replacementLoadout.Select(GetModKey).Distinct(StringComparer.Ordinal).Count() != 6 ||
                        !IsValidSetDistribution(replacementLoadout))
                    {
                        continue;
                    }

                    AddRosterCandidate(
                        candidates,
                        character,
                        result with
                        {
                            Mods = replacementLoadout.AsReadOnly(),
                            Status = "Valid alternative loadout considered by bounded global optimization."
                        });
                }
            }

            var orderedCandidates = candidates
                .OrderByDescending(candidate => candidate.Loadout.IsComplete)
                .ThenByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Key, StringComparer.Ordinal)
                .ToList();
            var requiredKeys = results
                .Select(result => string.Join(",", result.Mods
                    .Select(GetModKey)
                    .OrderBy(key => key, StringComparer.Ordinal)))
                .ToHashSet(StringComparer.Ordinal);
            var requiredCandidates = orderedCandidates
                .Where(candidate => requiredKeys.Contains(candidate.Key))
                .ToList();
            var optionalCandidates = orderedCandidates
                .Where(candidate => !requiredKeys.Contains(candidate.Key))
                .ToList();

            return requiredCandidates
                .Concat(optionalCandidates)
                .Take(GlobalOptimizationCandidateLimit)
                .ToList()
                .AsReadOnly();
        }

        private static void AddRosterCandidate(
            ICollection<RosterCandidate> candidates,
            CharacterEntity character,
            ModLoadoutResult loadout)
        {
            var candidate = new RosterCandidate(character, loadout);
            if (!candidates.Any(existing => string.Equals(existing.Key, candidate.Key, StringComparison.Ordinal)))
            {
                candidates.Add(candidate);
            }
        }

        private static IReadOnlyList<RosterCandidate> SelectGlobalRosterCandidates(
            IReadOnlyList<RosterCandidateGroup> groups,
            CancellationToken cancellationToken)
        {
            var selected = new List<RosterCandidate>(groups.Count);
            var usedModKeys = new HashSet<string>(StringComparer.Ordinal);
            var best = new List<RosterCandidate>();
            var bestQuality = RosterPlanQuality.Empty;
            var states = 0;

            SearchGlobalRosterCandidates(
                0,
                groups,
                selected,
                usedModKeys,
                cancellationToken,
                ref states,
                ref best,
                ref bestQuality);

            return best.AsReadOnly();
        }

        private static void SearchGlobalRosterCandidates(
            int index,
            IReadOnlyList<RosterCandidateGroup> groups,
            List<RosterCandidate> selected,
            HashSet<string> usedModKeys,
            CancellationToken cancellationToken,
            ref int states,
            ref List<RosterCandidate> best,
            ref RosterPlanQuality bestQuality)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++states > GlobalOptimizationStateLimit)
            {
                return;
            }

            if (index == groups.Count)
            {
                var quality = RosterPlanQuality.From(selected);
                if (quality.IsBetterThan(bestQuality))
                {
                    bestQuality = quality;
                    best = selected.ToList();
                }

                return;
            }

            foreach (var candidate in groups[index].Candidates)
            {
                var candidateKeys = candidate.Loadout.Mods.Select(GetModKey).ToList();
                if (candidateKeys.Any(usedModKeys.Contains))
                {
                    continue;
                }

                foreach (var key in candidateKeys)
                {
                    usedModKeys.Add(key);
                }

                selected.Add(candidate);
                SearchGlobalRosterCandidates(
                    index + 1,
                    groups,
                    selected,
                    usedModKeys,
                    cancellationToken,
                    ref states,
                    ref best,
                    ref bestQuality);
                selected.RemoveAt(selected.Count - 1);
                foreach (var key in candidateKeys)
                {
                    usedModKeys.Remove(key);
                }
            }
        }

        private static RosterLoadoutResult BuildRosterResult(
            IReadOnlyList<CharacterEntity> orderedCharacters,
            IReadOnlyList<GameModEntity> inventory,
            IReadOnlyDictionary<string, ModLoadoutResult> selectedByCharacterId,
            string status)
        {
            var initialInventoryBySlot = inventory
                .GroupBy(mod => mod.Slot)
                .ToDictionary(group => group.Key, group => group.Count());
            var remainingInventory = inventory.ToList();
            var plans = new List<RosterLoadoutPlan>(orderedCharacters.Count);
            var conflicts = new List<RosterLoadoutConflict>();

            foreach (var character in orderedCharacters)
            {
                var result = selectedByCharacterId.GetValueOrDefault(
                    character.Id,
                    new ModLoadoutResult(
                        Array.Empty<GameModEntity>(),
                        false,
                        false,
                        false,
                        "No loadout candidate was produced.",
                        Array.Empty<ModAssignmentExplanation>()));
                var assignedKeys = result.Mods.Select(GetModKey).ToHashSet(StringComparer.Ordinal);
                remainingInventory.RemoveAll(mod => assignedKeys.Contains(GetModKey(mod)));
                var characterConflicts = new List<RosterLoadoutConflict>();

                foreach (var slot in Enum.GetValues<ModSlot>())
                {
                    if (result.Mods.Any(mod => mod.Slot == (int)slot))
                    {
                        continue;
                    }

                    var hasRemainingMod = remainingInventory.Any(mod => mod.Slot == (int)slot);
                    var reason = !initialInventoryBySlot.TryGetValue((int)slot, out var initialCount) || initialCount == 0
                        ? $"No {slot} mod is available in the cached inventory."
                        : hasRemainingMod
                            ? $"A {slot} mod remains, but no valid complete loadout could use it with the set rules."
                            : $"No {slot} mod remained after the joint roster assignment reserved the available inventory.";
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

            var completeCount = plans.Count(plan => plan.Loadout.IsComplete);
            return new RosterLoadoutResult(
                plans.AsReadOnly(),
                conflicts.AsReadOnly(),
                plans.Count > 0 && completeCount == plans.Count,
                $"{status} Planned {plans.Count} character(s); {completeCount} complete loadout(s), {conflicts.Count} inventory conflict(s).");
        }

        private sealed record RosterCandidateGroup(
            CharacterEntity Character,
            IReadOnlyList<RosterCandidate> Candidates);

        private sealed record RosterCandidate(
            CharacterEntity Character,
            ModLoadoutResult Loadout)
        {
            public double Score => Loadout.Explanations.Sum(explanation => explanation.Score);

            public string Key => string.Join(",", Loadout.Mods
                .Select(GetModKey)
                .OrderBy(key => key, StringComparer.Ordinal));
        }

        private sealed record RosterPlanQuality(
            int CompleteCount,
            int CompletePriority,
            double Score,
            string TieBreaker)
        {
            public static RosterPlanQuality Empty { get; } = new(0, 0, double.MinValue, string.Empty);

            public static RosterPlanQuality From(IEnumerable<RosterCandidate> candidates)
            {
                var list = candidates.ToList();
                return new RosterPlanQuality(
                    list.Count(candidate => candidate.Loadout.IsComplete),
                    list.Where(candidate => candidate.Loadout.IsComplete).Sum(candidate => candidate.Character.Priority),
                    list.Sum(candidate => candidate.Score),
                    string.Join("|", list.Select(candidate => candidate.Key)));
            }

            public bool IsBetterThan(RosterPlanQuality other) =>
                CompleteCount > other.CompleteCount ||
                CompleteCount == other.CompleteCount && CompletePriority > other.CompletePriority ||
                CompleteCount == other.CompleteCount && CompletePriority == other.CompletePriority && Score > other.Score ||
                CompleteCount == other.CompleteCount && CompletePriority == other.CompletePriority &&
                Math.Abs(Score - other.Score) < 0.0001 &&
                string.CompareOrdinal(TieBreaker, other.TieBreaker) < 0;
        }

        private static void SearchValidLoadouts(
            int slotIndex,
            double currentScore,
            ModSlot[] slots,
            IReadOnlyDictionary<int, List<GameModEntity>> candidatesBySlot,
            IReadOnlyDictionary<string, double> targetSets,
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
            IReadOnlyDictionary<string, double> targetSets,
            IReadOnlyDictionary<int, IReadOnlyList<RecommendedPrimary>> targetPrimaries)
        {
            var score = (mod.Rarity * 10.0) + mod.Level + (mod.Tier * 2.0);
            score += ScoreSecondaryStats(mod);

            if (targetSets.TryGetValue(ModSetName(mod.Set), out var setPopularity))
            {
                score += setPopularity * 0.5;
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

        private static double ScoreSecondaryStats(GameModEntity mod)
        {
            if (string.IsNullOrWhiteSpace(mod.SecondaryStatsJson))
            {
                return 0;
            }

            if (SecondaryScoreCache.TryGetValue(mod.SecondaryStatsJson, out var cachedScore))
            {
                return cachedScore;
            }

            var score = 0.0;
            try
            {
                var snapshots = JsonSerializer.Deserialize<List<ModStatSnapshot>>(mod.SecondaryStatsJson);
                if (snapshots == null)
                {
                    return 0;
                }

                foreach (var snapshot in snapshots)
                {
                    if (string.Equals(snapshot.Type, nameof(StatType.Speed), StringComparison.OrdinalIgnoreCase))
                    {
                        score += snapshot.Value * 15.0;
                    }
                    else
                    {
                        score += Math.Min(Math.Abs(snapshot.Value), 100.0) * 0.05;
                    }

                    score += Math.Max(0, snapshot.RollCount - 1) * 0.5;
                }

                if (SecondaryScoreCache.Count >= SecondaryScoreCacheLimit)
                {
                    SecondaryScoreCache.Clear();
                }

                SecondaryScoreCache.TryAdd(mod.SecondaryStatsJson, score);
                return score;
            }
            catch (JsonException)
            {
                return 0;
            }
        }

        private async Task<IReadOnlyList<GameModEntity>> LoadCurrentEquippedModsAsync(
            string characterId,
            IReadOnlyList<GameModEntity> inventory,
            CancellationToken cancellationToken)
        {
            var allyCodes = inventory
                .Select(mod => mod.PlayerAllyCode)
                .Where(allyCode => !string.IsNullOrWhiteSpace(allyCode))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (allyCodes.Count != 1)
            {
                return Array.Empty<GameModEntity>();
            }

            return await _context.Mods
                .AsNoTracking()
                .Where(mod => mod.PlayerAllyCode == allyCodes[0] && mod.CharacterId == characterId)
                .OrderBy(mod => mod.Slot)
                .ThenBy(mod => mod.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        private static string GetScopedAllyCode(IReadOnlyList<GameModEntity> inventory)
        {
            var allyCodes = inventory
                .Select(mod => mod.PlayerAllyCode)
                .Where(allyCode => !string.IsNullOrWhiteSpace(allyCode))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            return allyCodes.Count == 1 ? allyCodes[0] : string.Empty;
        }

        private static ModLoadoutResult BuildResult(
            IReadOnlyList<GameModEntity> loadout,
            bool hasRecommendation,
            IReadOnlyDictionary<string, double> targetSets,
            IReadOnlyDictionary<int, IReadOnlyList<RecommendedPrimary>> targetPrimaries,
            string status,
            IReadOnlyDictionary<int, List<GameModEntity>>? candidatesBySlot = null,
            IReadOnlyList<GameModEntity>? currentEquippedMods = null)
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

            result = result with
            {
                Projection = BuildProjection(
                    currentEquippedMods ?? Array.Empty<GameModEntity>(),
                    loadout)
            };

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
                        explanation.Reason)
                    {
                        ScoreDelta = alternative.Score - selectedScore
                    });

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

        private static ModLoadoutProjection BuildProjection(
            IReadOnlyList<GameModEntity> currentEquippedMods,
            IReadOnlyList<GameModEntity> proposedMods)
        {
            if (currentEquippedMods.Count == 0)
            {
                return ModLoadoutProjection.Empty;
            }

            var currentTotals = SumPersistedModStats(currentEquippedMods);
            var proposedTotals = SumPersistedModStats(proposedMods);
            var impacts = currentTotals.Keys
                .Concat(proposedTotals.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(statName => statName, StringComparer.OrdinalIgnoreCase)
                .Select(statName =>
                {
                    currentTotals.TryGetValue(statName, out var currentValue);
                    proposedTotals.TryGetValue(statName, out var proposedValue);
                    return new ModStatImpact(statName, currentValue, proposedValue);
                })
                .Where(impact => Math.Abs(impact.Delta) > 0.0001)
                .ToList()
                .AsReadOnly();

            return new ModLoadoutProjection(true, impacts);
        }

        private static Dictionary<string, double> SumPersistedModStats(
            IEnumerable<GameModEntity> mods)
        {
            var totals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in mods)
            {
                AddPersistedStat(totals, mod.PrimaryStatType, mod.PrimaryStatValue);
                if (string.IsNullOrWhiteSpace(mod.SecondaryStatsJson))
                {
                    continue;
                }

                try
                {
                    var snapshots = JsonSerializer.Deserialize<List<ModStatSnapshot>>(mod.SecondaryStatsJson);
                    if (snapshots == null)
                    {
                        continue;
                    }

                    foreach (var snapshot in snapshots)
                    {
                        AddPersistedStat(totals, snapshot.Type, snapshot.Value);
                    }
                }
                catch (JsonException)
                {
                    // A malformed snapshot should not prevent the rest of the loadout from rendering.
                }
            }

            return totals;
        }

        private static void AddPersistedStat(
            IDictionary<string, double> totals,
            string? statName,
            double value)
        {
            if (string.IsNullOrWhiteSpace(statName) ||
                string.Equals(statName, "None", StringComparison.OrdinalIgnoreCase) ||
                double.IsNaN(value) ||
                double.IsInfinity(value))
            {
                return;
            }

            var normalizedName = statName.Trim();
            totals[normalizedName] = totals.GetValueOrDefault(normalizedName) + value;
        }

        private static ModAssignmentExplanation BuildExplanation(
            GameModEntity mod,
            IReadOnlyDictionary<string, double> targetSets,
            IReadOnlyDictionary<int, IReadOnlyList<RecommendedPrimary>> targetPrimaries)
        {
            var score = ScoreMod(mod, targetSets, targetPrimaries);
            var reasons = new List<string>();

            if (targetSets.TryGetValue(ModSetName(mod.Set), out var setPopularity))
            {
                reasons.Add($"matches the recommended {ModSetName(mod.Set)} set ({setPopularity:0.#}% usage)");
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
