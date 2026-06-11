#nullable enable

using System;
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
        private readonly AppDbContext _context;
        private readonly ILogger<ModAssignmentService> _logger;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

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
            ArgumentException.ThrowIfNullOrWhiteSpace(characterId);
            ArgumentNullException.ThrowIfNull(availableInventory);

            _logger.LogInformation("Calculating optimal mod loadout for character {CharacterId}", characterId);

            var recommendation = await _context.SwgohGgRecommendations
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.CharacterId == characterId, cancellationToken)
                .ConfigureAwait(false);

            var targetSets = DeserializeSets(recommendation);
            var targetPrimaries = DeserializePrimaries(recommendation);

            return availableInventory
                .GroupBy(m => m.Slot)
                .OrderBy(g => g.Key)
                .Select(g => g
                    .OrderByDescending(m => ScoreMod(m, targetSets, targetPrimaries))
                    .ThenByDescending(m => m.Rarity)
                    .ThenByDescending(m => m.Level)
                    .ThenByDescending(m => m.Tier)
                    .First())
                .Take(6)
                .ToList()
                .AsReadOnly();
        }

        private static double ScoreMod(
            GameModEntity mod,
            IReadOnlySet<string> targetSets,
            IReadOnlyDictionary<int, string> targetPrimaries)
        {
            var score = (mod.Rarity * 10.0) + mod.Level + (mod.Tier * 2.0);

            if (targetSets.Contains(ModSetName(mod.Set)))
            {
                score += 30.0;
            }

            if (targetPrimaries.ContainsKey(mod.Slot))
            {
                score += 50.0;
            }

            return score;
        }

        private static HashSet<string> DeserializeSets(SwgohGgRecommendationEntity? recommendation)
        {
            if (recommendation == null)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var sets = JsonSerializer.Deserialize<List<string>>(recommendation.SetRecommendationsJson, SerializerOptions);
            return new HashSet<string>(sets ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<int, string> DeserializePrimaries(SwgohGgRecommendationEntity? recommendation)
        {
            if (recommendation == null)
            {
                return new Dictionary<int, string>();
            }

            var primaries = JsonSerializer.Deserialize<Dictionary<string, string>>(recommendation.PrimaryStatsJson, SerializerOptions)
                ?? new Dictionary<string, string>();

            return primaries
                .Select(kvp => new KeyValuePair<int, string>(SlotNumber(kvp.Key), kvp.Value))
                .Where(kvp => kvp.Key > 0)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private static int SlotNumber(string slotName) => slotName switch
        {
            "Square" => (int)ModSlot.Square,
            "Arrow" => (int)ModSlot.Arrow,
            "Diamond" => (int)ModSlot.Diamond,
            "Triangle" => (int)ModSlot.Triangle,
            "Circle" => (int)ModSlot.Circle,
            "Cross" => (int)ModSlot.Cross,
            _ => 0
        };

        private static string ModSetName(int setId) => ((ModSet)setId).ToString();
    }
}
