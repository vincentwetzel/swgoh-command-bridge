#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Database.Entities;

namespace swgoh_command_bridge.Core.Services
{
    /// <summary>
    /// Implementation of IModAssignmentService that matches inventory mods to swgoh.gg recommendations.
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
            ArgumentNullException.ThrowIfNull(characterId);
            ArgumentNullException.ThrowIfNull(availableInventory);

            _logger.LogInformation("Calculating optimal mod loadout for character {CharacterId}", characterId);

            var recommendation = await _context.SwgohGgRecommendations
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.CharacterId == characterId, cancellationToken)
                .ConfigureAwait(false);

            var bestMods = new List<GameModEntity>(6);

            // Group available mods by slot to run fast slot-specific comparisons (Rule 18 optimization)
            var modsBySlot = new Dictionary<int, List<GameModEntity>>(6);
            for (int slot = 1; slot <= 6; slot++)
            {
                modsBySlot[slot] = new List<GameModEntity>();
            }

            foreach (var mod in availableInventory)
            {
                if (modsBySlot.TryGetValue(mod.Slot, out var list))
                {
                    list.Add(mod);
                }
            }

            for (int slot = 1; slot <= 6; slot++)
            {
                var candidates = modsBySlot[slot];
                if (candidates.Count == 0)
                {
                    continue;
                }

                GameModEntity? bestCandidate = null;
                double highestScore = -1.0;

                foreach (var candidate in candidates)
                {
                    double score = EvaluateCandidate(candidate, slot, recommendation);
                    if (score > highestScore)
                    {
                        highestScore = score;
                        bestCandidate = candidate;
                    }
                }

                if (bestCandidate != null)
                {
                    bestMods.Add(bestCandidate);
                }
            }

            _logger.LogInformation("Assigned {AssignedCount} optimal mods to character {CharacterId}", bestMods.Count, characterId);
            return bestMods.AsReadOnly();
        }

        private static double EvaluateCandidate(GameModEntity mod, int slot, SwgohGgRecommendationEntity? rec)
        {
            // Base score based on rarity, level, and tier (inherent mod quality)
            double score = (mod.Rarity * 10.0) + mod.Level + (mod.Tier * 2.0);

            if (rec == null)
            {
                // Roster coverage mode: prioritize simply putting a high-quality mod on the character
                return score;
            }

            // Check primary stat alignment for variable slots
            bool primaryMatch = slot switch
            {
                2 => MatchesPrimary(rec.ArrowPrimary, "Speed"), // default check speed arrow
                4 => MatchesPrimary(rec.TrianglePrimary, "Critical Damage") || MatchesPrimary(rec.TrianglePrimary, "Offense"),
                5 => MatchesPrimary(rec.CirclePrimary, "Protection") || MatchesPrimary(rec.CirclePrimary, "Health"),
                6 => MatchesPrimary(rec.CrossPrimary, "Offense") || MatchesPrimary(rec.CrossPrimary, "Potency"),
                _ => true // Slots 1 & 3 have fixed primaries
            };

            if (primaryMatch)
            {
                score += 50.0; // Substantial boost for matching the recommended primary stat
            }

            // Check set alignment (simplified check based on Set enum/id values)
            if (rec.RecommendedSets.Contains("Speed", StringComparison.OrdinalIgnoreCase) && mod.Set == 6)
            {
                score += 30.0;
            }
            else if (rec.RecommendedSets.Contains("Health", StringComparison.OrdinalIgnoreCase) && mod.Set == 1)
            {
                score += 20.0;
            }

            return score;
        }

        private static bool MatchesPrimary(string recommendedStr, string expectedStat)
        {
            return !string.IsNullOrEmpty(recommendedStr) && recommendedStr.Contains(expectedStat, StringComparison.OrdinalIgnoreCase);
        }
    }
}