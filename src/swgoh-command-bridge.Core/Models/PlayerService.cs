#nullable enable

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Database.Repositories;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services
{
    /// <summary>
    /// Implementation of IPlayerService that fetches player profiles and parses them.
    /// </summary>
    public class PlayerService : IPlayerService
    {
        private readonly IComlinkService _comlinkService;
        private readonly IPlayerRepository _playerRepository;
        private readonly ILogger<PlayerService> _logger;
        private readonly PlayerProfileParser _profileParser = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="PlayerService"/> class.
        /// </summary>
        public PlayerService(IComlinkService comlinkService, IPlayerRepository playerRepository, ILogger<PlayerService> logger)
        {
            ArgumentNullException.ThrowIfNull(comlinkService);
            ArgumentNullException.ThrowIfNull(playerRepository);
            ArgumentNullException.ThrowIfNull(logger);

            _comlinkService = comlinkService;
            _playerRepository = playerRepository;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<PlayerProfile> GetPlayerProfileAsync(string allyCode, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(allyCode);

            _logger.LogInformation("Retrieving profile for player with ally code {AllyCode}", allyCode);

            var rawJson = await _comlinkService.FetchPlayerRawAsync(allyCode, cancellationToken).ConfigureAwait(false);

            try
            {
                var profile = _profileParser.Parse(allyCode, rawJson);
                _logger.LogInformation(
                    "Successfully parsed profile for {PlayerName} with {CharacterCount} characters and {ModCount} mods",
                    profile.Name,
                    profile.Characters.Count,
                    profile.Mods.Count);
                return profile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing player profile raw data for ally code {AllyCode}", allyCode);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<PlayerProfile> SyncPlayerProfileAsync(string allyCode, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(allyCode);

            _logger.LogInformation("Starting live account sync for ally code {AllyCode}", allyCode);

            // Fetch fresh profile from Comlink API
            var profile = await GetPlayerProfileAsync(allyCode, cancellationToken).ConfigureAwait(false);

            // Map domain models into database-ready representation entities
            var entity = MapToEntity(profile);

            // Persist full configuration update atomically to local SQLite storage
            await _playerRepository.SavePlayerAsync(entity, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully completed account sync and cached profile updates in SQLite for {AllyCode}", allyCode);
            return profile;
        }

        private static PlayerEntity MapToEntity(PlayerProfile profile)
        {
            var entity = new PlayerEntity
            {
                AllyCode = profile.AllyCode,
                Name = profile.Name,
                Level = profile.Level,
                GalacticPower = profile.GalacticPower
            };

            foreach (var character in profile.Characters)
            {
                entity.Characters.Add(new CharacterEntity
                {
                    Id = character.Id,
                    PlayerAllyCode = profile.AllyCode,
                    Name = character.Name,
                    Level = character.Level,
                    Stars = character.Stars,
                    GearLevel = character.GearLevel,
                    GalacticPower = character.GalacticPower,
                    Priority = character.Priority,
                    Player = entity
                });
            }

            foreach (var mod in profile.Mods)
            {
                entity.Mods.Add(new GameModEntity
                {
                    Id = mod.Id,
                    PlayerAllyCode = profile.AllyCode,
                    CharacterId = mod.EquippedUnitId ?? string.Empty,
                    Set = (int)mod.Set,
                    Slot = (int)mod.Slot,
                    Level = mod.Level,
                    Tier = mod.Tier,
                    Rarity = mod.Pips,
                    PrimaryStatType = mod.Primary.Type.ToString(),
                    PrimaryStatValue = mod.Primary.Value,
                    SecondaryStatsJson = JsonSerializer.Serialize(
                        mod.Secondaries.ConvertAll(stat => new ModStatSnapshot(stat.Type.ToString(), stat.Value, stat.RollCount))),
                    Player = entity
                });
            }

            return entity;
        }

    }
}
