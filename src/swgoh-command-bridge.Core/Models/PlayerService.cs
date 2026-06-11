#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services
{
    /// <summary>
    /// Implementation of IPlayerService that fetches player profiles and parses them.
    /// </summary>
    public class PlayerService : IPlayerService
    {
        private readonly IComlinkService _comlinkService;
        private readonly ILogger<PlayerService> _logger;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="PlayerService"/> class.
        /// </summary>
        public PlayerService(IComlinkService comlinkService, ILogger<PlayerService> logger)
        {
            ArgumentNullException.ThrowIfNull(comlinkService);
            ArgumentNullException.ThrowIfNull(logger);

            _comlinkService = comlinkService;
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
                    foreach (var unit in rosterProp.EnumerateArray())
                    {
                        // Map units to Character and GameMod models
                        if (unit.TryGetProperty("definitionId", out var defIdProp))
                        {
                            var defId = defIdProp.GetString() ?? string.Empty;
                            // Character mapping logic can go here as needed
                        }

                        if (unit.TryGetProperty("equippedStatMod", out var modsProp) && modsProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var modJson in modsProp.EnumerateArray())
                            {
                                // GameMod mapping logic can go here as needed
                            }
                        }
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
    }
}