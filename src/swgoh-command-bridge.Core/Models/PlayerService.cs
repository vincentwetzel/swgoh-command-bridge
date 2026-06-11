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
                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "Unknown" : "Unknown";
                var level = root.TryGetProperty("level", out var levelProp) ? levelProp.GetInt32() : 0;

                long gp = 0;
                if (root.TryGetProperty("gp", out var gpProp))
                {
                    gp = gpProp.GetInt64();
                }

                var characters = new List<Character>();
                var mods = new List<GameMod>();

                // Parse roster if present
                if (root.TryGetProperty("rosterUnit", out var rosterProp) && rosterProp.ValueKind == JsonValueKind.Array)
                {
                    var rosterArray = rosterProp.EnumerateArray();
                    foreach (var unit in rosterArray)
                    {
                        if (!unit.TryGetProperty("definitionId", out var defIdProp))
                        {
                            continue;
                        }

                        var defId = defIdProp.GetString() ?? string.Empty;
                        var charId = defId.Split(':')[0];
                        var friendlyName = charId.Replace("_", " ", StringComparison.Ordinal);

                        var charLevel = unit.TryGetProperty("currentLevel", out var lvProp) ? lvProp.GetInt32() : 1;
                        var gearLevel = unit.TryGetProperty("currentGearLevel", out var gearProp) ? gearProp.GetInt32() : 1;

                        var relicTier = 0;
                        if (unit.TryGetProperty("relic", out var relicProp) && relicProp.TryGetProperty("currentTier", out var tierProp))
                        {
                            var rawRelic = tierProp.GetInt32();
                            relicTier = rawRelic > 2 ? rawRelic - 2 : 0;
                        }

                        var unitGp = unit.TryGetProperty("gp", out var unitGpProp) ? unitGpProp.GetInt32() : 0;
                        var equippedMods = new Dictionary<ModSlot, GameMod>();

                        if (unit.TryGetProperty("equippedStatMod", out var modsProp) && modsProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var modJson in modsProp.EnumerateArray())
                            {
                                var parsedMod = ParseGameMod(modJson, charId);
                                if (parsedMod != null)
                                {
                                    equippedMods[parsedMod.Slot] = parsedMod;
                                    mods.Add(parsedMod);
                                }
                            }
                        }

                        var character = new Character(
                            Id: charId,
                            Name: friendlyName,
                            Level: charLevel,
                            GearLevel: gearLevel,
                            RelicTier: relicTier,
                            GalacticPower: unitGp,
                            Priority: 0,
                            EquippedMods: equippedMods
                        );

                        characters.Add(character);
                    }
                }

                _logger.LogInformation("Successfully parsed profile for {PlayerName} with {CharacterCount} characters and {ModCount} mods", name, characters.Count, mods.Count);

                return new PlayerProfile(
                    AllyCode: allyCode,
                    Name: name,
                    Level: level,
                    GalacticPower: gp,
                    Characters: characters.AsReadOnly(),
                    Mods: mods.AsReadOnly()
                );
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
                    Stars = 7, // Standard default stars count
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
                    Player = entity
                });
            }

            return entity;
        }

        private static GameMod? ParseGameMod(JsonElement modJson, string? equippedUnitId)
        {
            try
            {
                if (!modJson.TryGetProperty("id", out var idProp))
                {
                    return null;
                }

                var id = idProp.GetString() ?? Guid.NewGuid().ToString();
                var level = modJson.TryGetProperty("level", out var lvlProp) ? lvlProp.GetInt32() : 1;
                var pips = modJson.TryGetProperty("pips", out var pipsProp) ? pipsProp.GetInt32() : 5;
                var tier = modJson.TryGetProperty("tier", out var tierProp) ? tierProp.GetInt32() : 1;

                var slotVal = modJson.TryGetProperty("slot", out var slotProp) ? slotProp.GetInt32() : 1;
                var setVal = modJson.TryGetProperty("set", out var setProp) ? setProp.GetInt32() : 1;

                var slot = (ModSlot)slotVal;
                var set = (ModSet)setVal;

                ModStat? primary = null;
                if (modJson.TryGetProperty("primaryStat", out var primaryProp) && primaryProp.TryGetProperty("stat", out var statProp))
                {
                    var unitId = statProp.GetProperty("unitId").GetInt32();
                    var rawValue = statProp.GetProperty("value").GetInt64();
                    primary = new ModStat((StatType)unitId, rawValue / 100000000.0, 1);
                }

                if (primary == null)
                {
                    primary = new ModStat(StatType.None, 0);
                }

                var secondaries = new List<ModStat>(4);
                if (modJson.TryGetProperty("secondaryStat", out var secondaryProp) && secondaryProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var secJson in secondaryProp.EnumerateArray())
                    {
                        if (secJson.TryGetProperty("stat", out var secStatProp))
                        {
                            var unitId = secStatProp.GetProperty("unitId").GetInt32();
                            var rawValue = secStatProp.GetProperty("value").GetInt64();
                            var rollCount = secJson.TryGetProperty("roll", out var rollProp) ? rollProp.GetInt32() : 1;
                            secondaries.Add(new ModStat((StatType)unitId, rawValue / 100000000.0, rollCount));
                        }
                    }
                }

                return new GameMod(
                    Id: id,
                    Level: level,
                    Pips: pips,
                    Tier: tier,
                    Slot: slot,
                    Set: set,
                    Primary: primary,
                    Secondaries: secondaries,
                    EquippedUnitId: equippedUnitId
                );
            }
            catch
            {
                return null;
            }
        }
    }
}