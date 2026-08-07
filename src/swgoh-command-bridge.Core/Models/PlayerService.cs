#nullable enable

using System;
using System.Collections.Generic;
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
        private readonly CharacterMetadataParser _metadataParser = new();

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
            allyCode = AllyCodeValidator.NormalizeOrThrow(allyCode);

            _logger.LogInformation("Retrieving profile for player with ally code {AllyCode}", allyCode);

            var rawJson = await _comlinkService.FetchPlayerRawAsync(allyCode, cancellationToken).ConfigureAwait(false);

            try
            {
                var profile = _profileParser.Parse(allyCode, rawJson);
                _logger.LogInformation(
                    "Successfully parsed profile for {PlayerName} with {CharacterCount} characters and {ModCount} mods; {WarningCount} parser warning(s)",
                    profile.Name,
                    profile.Characters.Count,
                    profile.Mods.Count,
                    profile.Diagnostics.Warnings.Count);
                return profile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing player profile raw data for ally code {AllyCode}", allyCode);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<PlayerProfile> SyncPlayerProfileAsync(
            string allyCode,
            CancellationToken cancellationToken = default,
            IProgress<PlayerSyncProgress>? progress = null)
        {
            allyCode = AllyCodeValidator.NormalizeOrThrow(allyCode);

            _logger.LogInformation("Starting live account sync for ally code {AllyCode}", allyCode);
            progress?.Report(new PlayerSyncProgress(
                "connecting",
                "Connecting to Comlink...",
                0,
                4));

            // Fetch fresh profile from Comlink API
            var profile = await GetPlayerProfileForSyncAsync(allyCode, cancellationToken).ConfigureAwait(false);
            progress?.Report(new PlayerSyncProgress(
                "mapping",
                $"Mapped {profile.Characters.Count} characters and {profile.Mods.Count} mods.",
                1,
                4));

            // Map domain models into database-ready representation entities
            var entity = MapToEntity(profile);
            progress?.Report(new PlayerSyncProgress(
                "persisting",
                "Saving the refreshed account cache...",
                2,
                4));

            // Persist full configuration update atomically to local SQLite storage
            await _playerRepository.SavePlayerAsync(entity, cancellationToken).ConfigureAwait(false);

            progress?.Report(new PlayerSyncProgress(
                "complete",
                "Account cache saved successfully.",
                4,
                4));
            _logger.LogInformation("Successfully completed account sync and cached profile updates in SQLite for {AllyCode}", allyCode);
            return profile;
        }

        private async Task<PlayerProfile> GetPlayerProfileForSyncAsync(
            string allyCode,
            CancellationToken cancellationToken)
        {
            var rawJson = await _comlinkService.FetchPlayerRawAsync(allyCode, cancellationToken).ConfigureAwait(false);
            IReadOnlyDictionary<string, string>? metadataNames = null;
            string? metadataWarning = null;

            try
            {
                var metadataJson = await _comlinkService.FetchMetaDataRawAsync(cancellationToken).ConfigureAwait(false);
                metadataNames = _metadataParser.Parse(metadataJson);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                metadataWarning = $"Character display metadata was unavailable; roster IDs were retained ({ex.GetType().Name}).";
                _logger.LogWarning(ex, "Comlink metadata enrichment failed; continuing with player payload names");
            }

            var profile = _profileParser.Parse(allyCode, rawJson, metadataNames);
            if (metadataWarning == null)
            {
                return profile;
            }

            var warnings = new List<string>(profile.Diagnostics.Warnings)
            {
                metadataWarning
            };
            return profile with
            {
                Diagnostics = profile.Diagnostics with
                {
                    Warnings = warnings.AsReadOnly()
                }
            };
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
