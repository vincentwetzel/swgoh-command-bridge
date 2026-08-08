#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Database.Repositories;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using swgoh_command_bridge.Tests.Fixtures;
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
            var payload = ComlinkPayloadFixtures.ValidRosterAndEquippedMod;

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
            var payload = ComlinkPayloadFixtures.EmptyRoster;

            var fakeComlink = new FakeComlinkService(payload);
            var fakeRepo = new FakePlayerRepository();
            var fakeHistory = new FakeSyncHistoryRepository();
            var service = new PlayerService(
                fakeComlink,
                fakeRepo,
                NullLogger<PlayerService>.Instance,
                fakeHistory);

            // Act
            var result = await service.SyncPlayerProfileAsync("987654321", CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Kenobi", result.Name);
            Assert.NotNull(fakeRepo.SavedPlayer);
            Assert.Equal("987654321", fakeRepo.SavedPlayer!.AllyCode);
            Assert.Equal("Kenobi", fakeRepo.SavedPlayer.Name);
            Assert.Equal("completed", fakeHistory.Status);
            Assert.Equal(0, fakeHistory.CharacterCount);
            Assert.Equal(0, fakeHistory.ModCount);
            Assert.Equal(85, fakeRepo.SavedPlayer.Level);
            Assert.Equal(5100000, fakeRepo.SavedPlayer.GalacticPower);
            Assert.NotNull(fakeRepo.SavedPlayer.LastSyncedUtc);
            Assert.InRange(
                fakeRepo.SavedPlayer.LastSyncedUtc!.Value,
                DateTime.UtcNow.AddMinutes(-1),
                DateTime.UtcNow.AddMinutes(1));
        }

        [Fact]
        public async Task SyncPlayerProfileAsync_ReportsProgressPhases()
        {
            var progressUpdates = new List<PlayerSyncProgress>();
            var fakeRepo = new FakePlayerRepository();
            var service = new PlayerService(
                new FakeComlinkService(ComlinkPayloadFixtures.EmptyRoster),
                fakeRepo,
                NullLogger<PlayerService>.Instance);

            await service.SyncPlayerProfileAsync(
                "123456789",
                progress: new InlineProgress<PlayerSyncProgress>(progressUpdates.Add));

            Assert.Equal(
                new[] { "connecting", "mapping", "persisting", "complete" },
                progressUpdates.Select(update => update.Phase));
            Assert.Equal(4, progressUpdates[^1].CompletedSteps);
            Assert.Equal(4, progressUpdates[^1].TotalSteps);
        }

        [Fact]
        public async Task SyncPlayerProfileAsync_WithInventoryMods_PreservesStarsAndStatSnapshots()
        {
            var payload = ComlinkPayloadFixtures.InventoryMods;

            var fakeRepo = new FakePlayerRepository();
            var service = new PlayerService(
                new FakeComlinkService(payload),
                fakeRepo,
                NullLogger<PlayerService>.Instance);

            await service.SyncPlayerProfileAsync("111222333");

            Assert.NotNull(fakeRepo.SavedPlayer);
            Assert.Equal(5, fakeRepo.SavedPlayer!.Characters.Single().Stars);
            var savedMod = Assert.Single(fakeRepo.SavedPlayer.Mods);
            Assert.Equal("Health", savedMod.PrimaryStatType);
            Assert.Equal("111222333", savedMod.PlayerAllyCode);
            Assert.Equal(string.Empty, savedMod.CharacterId);
            Assert.Equal(1, savedMod.Slot);
            Assert.Equal(4, savedMod.Set);
            Assert.Equal(15, savedMod.Level);
            Assert.Equal(5, savedMod.Tier);
            Assert.Equal(6, savedMod.Rarity);
            Assert.Equal(1, savedMod.PrimaryStatValue);
            Assert.Contains("Speed", savedMod.SecondaryStatsJson);
            Assert.Contains("3", savedMod.SecondaryStatsJson);
        }

        [Fact]
        public async Task SyncPlayerProfileAsync_UsesComlinkMetadataNamesWithoutMakingMetadataMandatory()
        {
            var fakeRepo = new FakePlayerRepository();
            var service = new PlayerService(
                new FakeComlinkService(
                    ComlinkPayloadFixtures.RosterForMetadataEnrichment,
                    ComlinkPayloadFixtures.MetadataCatalog),
                fakeRepo,
                NullLogger<PlayerService>.Instance);

            var result = await service.SyncPlayerProfileAsync("123456789");

            Assert.Equal("Rey", Assert.Single(result.Characters).Name);
            Assert.False(result.Diagnostics.HasWarnings);
        }

        [Fact]
        public async Task GetPlayerProfileAsync_WithPartialMixedShapePayload_PreservesUsableRecords()
        {
            var payload = ComlinkPayloadFixtures.MalformedAndDuplicateRecords;

            var service = new PlayerService(
                new FakeComlinkService(payload),
                new FakePlayerRepository(),
                NullLogger<PlayerService>.Instance);

            var result = await service.GetPlayerProfileAsync("444555666");

            var character = Assert.Single(result.Characters);
            Assert.Equal("LUKE_SKYWALKER", character.Id);
            Assert.Equal("Luke Skywalker", character.Name);
            Assert.Equal(1, character.GearLevel);
            Assert.Equal(7, character.Stars);
            Assert.Equal(2, result.Mods.Count);
            Assert.Contains(result.Mods, mod => mod.Id == "shared-mod" && mod.Primary.Type == StatType.Speed);
            Assert.Contains(result.Mods, mod => mod.Id == "inventory-mod" && mod.Pips == 6);
            Assert.Equal(2, result.Diagnostics.RosterRecordsSeen);
            Assert.Equal(1, result.Diagnostics.RosterRecordsSkipped);
            Assert.Equal(2, result.Diagnostics.InventoryRecordsSeen);
            Assert.Equal(1, result.Diagnostics.DuplicateModsSkipped);
            Assert.True(result.Diagnostics.HasWarnings);
        }

        [Fact]
        public async Task GetPlayerProfileAsync_WithNestedCharacterMetadataAndMalformedEquippedMod_UsesMetadataAndReportsLoss()
        {
            var payload = ComlinkPayloadFixtures.NestedMetadataAndMalformedEquippedMod;

            var service = new PlayerService(
                new FakeComlinkService(payload),
                new FakePlayerRepository(),
                NullLogger<PlayerService>.Instance);

            var result = await service.GetPlayerProfileAsync("555666777");

            var character = Assert.Single(result.Characters);
            Assert.Equal("REY", character.Id);
            Assert.Equal("Rey", character.Name);
            Assert.Single(result.Mods);
            Assert.Equal(2, result.Diagnostics.EquippedModRecordsSeen);
            Assert.Equal(1, result.Diagnostics.EquippedModRecordsSkipped);
            Assert.Contains(result.Diagnostics.Warnings, warning =>
                warning.Contains("equipped mod", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task GetPlayerProfileAsync_WithNestedEnvelopeAndInventoryAliasesMapsCompleteSnapshot()
        {
            var service = new PlayerService(
                new FakeComlinkService(ComlinkPayloadFixtures.NestedEnvelopeWithUnequippedInventory),
                new FakePlayerRepository(),
                NullLogger<PlayerService>.Instance);

            var result = await service.GetPlayerProfileAsync("222333444");

            Assert.Equal("Envelope Player", result.Name);
            Assert.Equal(85, result.Level);
            Assert.Equal(2345678, result.GalacticPower);
            var character = Assert.Single(result.Characters);
            Assert.Equal("PADME_AMIDALA", character.Id);
            Assert.Equal("Padme Amidala", character.Name);
            Assert.Equal(7, character.Stars);
            Assert.Equal(13, character.GearLevel);
            Assert.Equal(3, character.RelicTier);
            Assert.Equal(2, result.Mods.Count);

            var equipped = Assert.Single(result.Mods, mod => mod.Id == "envelope-equipped");
            Assert.Equal(ModSlot.Arrow, equipped.Slot);
            Assert.Equal(ModSet.Speed, equipped.Set);
            Assert.Equal(StatType.Speed, equipped.Primary.Type);
            Assert.Equal(30, equipped.Primary.Value);
            Assert.Equal(2, equipped.Secondaries[0].RollCount);
            Assert.Equal("PADME_AMIDALA", equipped.EquippedUnitId);

            var inventory = Assert.Single(result.Mods, mod => mod.Id == "envelope-inventory");
            Assert.Equal(ModSlot.Square, inventory.Slot);
            Assert.Equal(ModSet.Health, inventory.Set);
            Assert.Equal(StatType.OffensePercent, inventory.Primary.Type);
            Assert.Null(inventory.EquippedUnitId);
            Assert.False(result.Diagnostics.HasWarnings);
        }

        private class FakeComlinkService : IComlinkService
        {
            private readonly string _response;
            private readonly string _metadataResponse;

            public FakeComlinkService(string response, string metadataResponse = "{}")
            {
                _response = response;
                _metadataResponse = metadataResponse;
            }

            public Task<string> FetchPlayerRawAsync(string allyCode, CancellationToken cancellationToken = default) => Task.FromResult(_response);

            public Task<string> FetchMetaDataRawAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(_metadataResponse);
        }

        private sealed class InlineProgress<T> : IProgress<T>
        {
            private readonly Action<T> _handler;

            public InlineProgress(Action<T> handler)
            {
                _handler = handler;
            }

            public void Report(T value) => _handler(value);
        }

        private class FakePlayerRepository : IPlayerRepository
        {
            public PlayerEntity? SavedPlayer { get; private set; }

            public Task SavePlayerAsync(PlayerEntity player, CancellationToken cancellationToken = default)
            {
                SavedPlayer = player;
                return Task.CompletedTask;
            }

            public Task<bool> DeletePlayerAsync(string allyCode, CancellationToken cancellationToken = default) =>
                Task.FromResult(false);
        }

        private sealed class FakeSyncHistoryRepository : ISyncHistoryRepository
        {
            public string? Status { get; private set; }
            public int CharacterCount { get; private set; }
            public int ModCount { get; private set; }

            public Task<long> StartAsync(
                string allyCode,
                DateTime startedUtc,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(1L);

            public Task CompleteAsync(
                long id,
                DateTime completedUtc,
                int characterCount,
                int modCount,
                int warningCount,
                CancellationToken cancellationToken = default)
            {
                Status = "completed";
                CharacterCount = characterCount;
                ModCount = modCount;
                return Task.CompletedTask;
            }

            public Task FinishAsync(
                long id,
                DateTime completedUtc,
                string status,
                string errorSummary,
                CancellationToken cancellationToken = default)
            {
                Status = status;
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<SyncHistoryEntity>> GetRecentAsync(
                string allyCode,
                int limit = 10,
                CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<SyncHistoryEntity>>(Array.Empty<SyncHistoryEntity>());
        }
    }
}
