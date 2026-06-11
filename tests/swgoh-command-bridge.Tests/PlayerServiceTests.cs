#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Database.Repositories;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests
{
    /// <summary>
    /// Unit tests verifying comlink JSON parsing and database mapping pathways in PlayerService.
    /// </summary>
    public class PlayerServiceTests
    {
        [Fact]
        public async Task GetPlayerProfileAsync_WithValidJsonPayload_ParsesRosterAndModsCorrectly()
        {
            // Arrange
            var payload = @"
            {
                ""name"": ""Skywalker"",
                ""level"": 85,
                ""gp"": 4200000,
                ""rosterUnit"": [
                    {
                        ""definitionId"": ""DARTHTRAYA:SEVEN_STAR"",
                        ""currentLevel"": 85,
                        ""currentGearLevel"": 12,
                        ""relic"": {
                            ""currentTier"": 5
                        },
                        ""gp"": 24500,
                        ""equippedStatMod"": [
                          {
                            ""id"": ""mod_speed_test"",
                            ""level"": 15,
                            ""pips"": 6,
                            ""tier"": 5,
                            ""slot"": 2,
                            ""set"": 1,
                            ""primaryStat"": {
                              ""stat"": {
                                ""unitId"": 5,
                                ""value"": 3000000000
                              }
                            },
                            ""secondaryStat"": [
                              {
                                ""stat"": {
                                  ""unitId"": 5,
                                  ""value"": 1500000000
                                },
                                ""roll"": 2
                              }
                            ]
                          }
                        ]
                    }
                ]
            }";

            var fakeComlink = new FakeComlinkService(payload);
            var fakeRepo = new FakePlayerRepository();
            var service = new PlayerService(fakeComlink, fakeRepo, NullLogger<PlayerService>.Instance);

            // Act
            var result = await service.GetPlayerProfileAsync("123456789", CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Skywalker", result.Name);
            Assert.Equal(85, result.Level);
            Assert.Equal(4200000, result.GalacticPower);
            Assert.Single(result.Characters);

            var trayChar = result.Characters[0];
            Assert.Equal("DARTHTRAYA", trayChar.Id);
            Assert.Equal(85, trayChar.Level);
            Assert.Equal(12, trayChar.GearLevel);
            Assert.Equal(3, trayChar.RelicTier); // 5 - 2 = 3
            Assert.Equal(24500, trayChar.GalacticPower);
            Assert.Single(result.Mods);

            var mod = result.Mods[0];
            Assert.Equal("mod_speed_test", mod.Id);
            Assert.Equal(15, mod.Level);
            Assert.Equal(6, mod.Pips);
            Assert.Equal(5, mod.Tier);
            Assert.Equal(ModSlot.Arrow, mod.Slot); // Slot 2 -> Arrow
            Assert.Equal(ModSet.Health, mod.Set); // Set 1 -> Health
            Assert.Equal(StatType.Speed, mod.Primary.Type); // unitId 5 -> Speed
            Assert.Equal(30.0, mod.Primary.Value); // 3000000000 / 100000000.0 = 30.0
            Assert.Single(mod.Secondaries);
            Assert.Equal(StatType.Speed, mod.Secondaries[0].Type);
            Assert.Equal(15.0, mod.Secondaries[0].Value); // 1500000000 / 100000000.0 = 15.0
            Assert.Equal(2, mod.Secondaries[0].RollCount);
        }

        [Fact]
        public async Task SyncPlayerProfileAsync_WhenInvoked_SavesToRepositoryCorrectly()
        {
            // Arrange
            var payload = @"
            {
                ""name"": ""Kenobi"",
                ""level"": 85,
                ""gp"": 5100000,
                ""rosterUnit"": []
            }";

            var fakeComlink = new FakeComlinkService(payload);
            var fakeRepo = new FakePlayerRepository();
            var service = new PlayerService(fakeComlink, fakeRepo, NullLogger<PlayerService>.Instance);

            // Act
            var result = await service.SyncPlayerProfileAsync("987654321", CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Kenobi", result.Name);
            Assert.NotNull(fakeRepo.SavedPlayer);
            Assert.Equal("987654321", fakeRepo.SavedPlayer!.AllyCode);
            Assert.Equal("Kenobi", fakeRepo.SavedPlayer.Name);
            Assert.Equal(85, fakeRepo.SavedPlayer.Level);
            Assert.Equal(5100000, fakeRepo.SavedPlayer.GalacticPower);
        }

        private class FakeComlinkService : IComlinkService
        {
            private readonly string _response;

            public FakeComlinkService(string response)
            {
                _response = response;
            }

            public Task<string> FetchPlayerRawAsync(string allyCode, CancellationToken cancellationToken = default) => Task.FromResult(_response);
        }

        private class FakePlayerRepository : IPlayerRepository
        {
            public PlayerEntity? SavedPlayer { get; private set; }

            public Task SavePlayerAsync(PlayerEntity player, CancellationToken cancellationToken = default)
            {
                SavedPlayer = player;
                return Task.CompletedTask;
            }
        }
    }
}