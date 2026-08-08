#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class ModAssignmentServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    public ModAssignmentServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task CalculateOptimalLoadoutAsync_PrefersAValidSixModSetDistribution()
    {
        var inventory = new List<GameModEntity>
        {
            CreateMod("greedy-1", 1, 1, 6),
            CreateMod("greedy-2", 2, 3, 6),
            CreateMod("greedy-3", 3, 4, 6),
            CreateMod("greedy-4", 4, 2, 6),
            CreateMod("greedy-5", 5, 5, 6),
            CreateMod("greedy-6", 6, 7, 6),
            CreateMod("valid-1", 1, 1, 5),
            CreateMod("valid-2", 2, 1, 5),
            CreateMod("valid-3", 3, 2, 5),
            CreateMod("valid-4", 4, 2, 5),
            CreateMod("valid-5", 5, 2, 5),
            CreateMod("valid-6", 6, 2, 5)
        };
        var service = new ModAssignmentService(
            _context,
            NullLogger<ModAssignmentService>.Instance);

        var result = await service.CalculateOptimalLoadoutAsync("CHARACTER", inventory);

        Assert.Equal(6, result.Count);
        Assert.Equal(2, result.Count(mod => mod.Set == 1));
        Assert.Equal(4, result.Count(mod => mod.Set == 2));
    }

    [Fact]
    public async Task CalculateOptimalLoadoutResultAsync_ReportsIncompleteInventoryAndReasons()
    {
        var inventory = new List<GameModEntity>
        {
            CreateMod("only-square", 1, 1, 5),
            CreateMod("only-arrow", 2, 1, 5)
        };
        var service = new ModAssignmentService(
            _context,
            NullLogger<ModAssignmentService>.Instance);

        var result = await service.CalculateOptimalLoadoutResultAsync("CHARACTER", inventory);

        Assert.False(result.HasRecommendation);
        Assert.False(result.IsComplete);
        Assert.False(result.MeetsSetRules);
        Assert.Contains("Not enough inventory", result.Status);
        Assert.Equal(2, result.Mods.Count);
        Assert.Equal(2, result.Explanations.Count);
        Assert.All(result.Explanations, explanation => Assert.Contains("score", explanation.Reason));
    }

    [Fact]
    public async Task CalculateOptimalLoadoutResultAsync_ReportsAlternativeCandidatesPerSlot()
    {
        var inventory = Enumerable.Range(1, 6)
            .Select(index => CreateMod(
                $"selected-{index}",
                index,
                index <= 2 ? 1 : 2,
                6))
            .Append(CreateMod("alternative-square", 1, 4, 5))
            .ToList();
        var service = new ModAssignmentService(
            _context,
            NullLogger<ModAssignmentService>.Instance);

        var result = await service.CalculateOptimalLoadoutResultAsync("CHARACTER", inventory);

        Assert.True(result.IsComplete);
        Assert.Contains(result.Alternatives, alternative => alternative.ModId == "alternative-square");
        Assert.All(result.Alternatives, alternative => Assert.InRange(alternative.Slot, 1, 6));
        Assert.All(result.Alternatives, alternative => Assert.False(string.IsNullOrWhiteSpace(alternative.BenefitSummary)));
    }

    [Fact]
    public async Task CalculateOptimalLoadoutResultAsync_UsesPersistedRecommendationProvenance()
    {
        _context.SwgohGgRecommendations.Add(new SwgohGgRecommendationEntity
        {
            CharacterId = "CHARACTER",
            PlayerAllyCode = "123456789",
            Source = "fixture-source",
            RecommendationSchemaVersion = 1,
            SourceUrl = "https://example.test/character",
            SetRecommendationsJson =
                $"[{{\"name\":\"{ModSet.Health}\",\"percentage\":80}}]",
            PrimaryStatsJson =
                "{\"Slot 1\":[{\"statName\":\"Speed\",\"percentage\":95}]}"
        });
        await _context.SaveChangesAsync();

        var inventory = Enumerable.Range(1, 6)
            .Select(index => CreateMod(
                $"recommended-{index}",
                index,
                index == 1 ? 1 : 2,
                6))
            .ToList();
        inventory[0].PrimaryStatType = "Speed";

        var service = new ModAssignmentService(
            _context,
            NullLogger<ModAssignmentService>.Instance);

        var result = await service.CalculateOptimalLoadoutResultAsync("CHARACTER", inventory);

        Assert.True(result.HasRecommendation);
        Assert.True(result.IsComplete);
        Assert.True(result.MeetsSetRules);
        Assert.Contains(result.Explanations, explanation =>
            explanation.Reason.Contains("recommended", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CalculateOptimalLoadoutResultAsync_UsesSetPopularityToRankAlternatives()
    {
        _context.SwgohGgRecommendations.Add(new SwgohGgRecommendationEntity
        {
            CharacterId = "POPULARITY",
            PlayerAllyCode = "123456789",
            Source = "fixture-source",
            RecommendationSchemaVersion = 1,
            SetRecommendationsJson =
                $"[{{\"name\":\"{ModSet.Health}\",\"percentage\":80}},{{\"name\":\"{ModSet.Speed}\",\"percentage\":20}}]",
            PrimaryStatsJson = "{}"
        });
        await _context.SaveChangesAsync();

        var inventory = Enumerable.Range(1, 6)
            .Select(index => CreateMod($"health-{index}", index, (int)ModSet.Health, 5))
            .ToList();
        inventory.Add(CreateMod("speed-square", 1, (int)ModSet.Speed, 5));

        var service = new ModAssignmentService(
            _context,
            NullLogger<ModAssignmentService>.Instance);

        var result = await service.CalculateOptimalLoadoutResultAsync("POPULARITY", inventory);

        Assert.True(result.IsComplete);
        Assert.Equal("health-1", result.Mods.Single(mod => mod.Slot == 1).Id);
        Assert.Contains("80% usage", result.Explanations.Single(explanation => explanation.ModId == "health-1").Reason);
    }

    [Fact]
    public async Task CalculateOptimalLoadoutResultAsync_IgnoresRecommendationFromAnotherAllyCode()
    {
        _context.SwgohGgRecommendations.Add(new SwgohGgRecommendationEntity
        {
            CharacterId = "CHARACTER",
            PlayerAllyCode = "987654321",
            Source = "other-account",
            RecommendationSchemaVersion = 1,
            SourceUrl = "https://example.test/other-account",
            SetRecommendationsJson =
                $"[{{\"name\":\"{ModSet.Health}\",\"percentage\":80}}]",
            PrimaryStatsJson =
                "{\"Slot 1\":[{\"statName\":\"Speed\",\"percentage\":95}]}"
        });
        await _context.SaveChangesAsync();

        var inventory = Enumerable.Range(1, 6)
            .Select(index => CreateMod(
                $"isolated-{index}",
                index,
                index == 1 ? 1 : 2,
                6))
            .ToList();
        inventory[0].PrimaryStatType = "Speed";

        var service = new ModAssignmentService(
            _context,
            NullLogger<ModAssignmentService>.Instance);

        var result = await service.CalculateOptimalLoadoutResultAsync("CHARACTER", inventory);

        Assert.False(result.HasRecommendation);
        Assert.DoesNotContain(result.Explanations, explanation =>
            explanation.Reason.Contains("recommended", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CalculateOptimalLoadoutResultAsync_ProjectsPersistedModStatDeltas()
    {
        _context.Mods.Add(new GameModEntity
        {
            Id = "current-speed",
            PlayerAllyCode = "123456789",
            CharacterId = "CHARACTER",
            Slot = 1,
            Set = 1,
            Rarity = 5,
            PrimaryStatType = "Speed",
            PrimaryStatValue = 10,
            SecondaryStatsJson = "[{\"type\":\"HealthPercent\",\"value\":5,\"rollCount\":1}]"
        });
        await _context.SaveChangesAsync();

        var inventory = Enumerable.Range(1, 6)
            .Select(index => CreateMod($"projected-{index}", index, index <= 2 ? 1 : 2, 5))
            .ToList();
        inventory[0].PrimaryStatType = "Speed";
        inventory[0].PrimaryStatValue = 20;

        var service = new ModAssignmentService(
            _context,
            NullLogger<ModAssignmentService>.Instance);

        var result = await service.CalculateOptimalLoadoutResultAsync("CHARACTER", inventory);

        Assert.True(result.Projection.HasCurrentEquippedMods);
        var speedImpact = Assert.Single(result.Projection.StatImpacts
            .Where(impact => impact.StatName == "Speed"));
        Assert.Equal(10, speedImpact.CurrentValue);
        Assert.Equal(20, speedImpact.ProposedValue);
        Assert.Equal(10, speedImpact.Delta);
        Assert.Contains("base character stats", result.Projection.Disclaimer);
    }

    [Fact]
    public async Task CalculateRosterLoadoutsAsync_ReservesEachModOnceAndHonorsPriority()
    {
        var inventory = Enumerable.Range(1, 12)
            .Select(index => CreateMod($"roster-{index}", ((index - 1) % 6) + 1, index % 2 == 0 ? 1 : 2, 5))
            .ToList();
        var characters = new[]
        {
            new CharacterEntity { Id = "LOW", Name = "Lower Priority", Priority = 5 },
            new CharacterEntity { Id = "HIGH", Name = "Higher Priority", Priority = 10 }
        };
        var service = new ModAssignmentService(
            _context,
            NullLogger<ModAssignmentService>.Instance);

        var result = await service.CalculateRosterLoadoutsAsync(characters, inventory);

        Assert.Equal(new[] { "HIGH", "LOW" }, result.Plans.Select(plan => plan.CharacterId));
        Assert.Equal(12, result.Plans.SelectMany(plan => plan.Loadout.Mods).Select(mod => mod.Id).Distinct().Count());
        Assert.All(result.Plans, plan => Assert.Equal(6, plan.Loadout.Mods.Count));
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task CalculateRosterLoadoutsAsync_ReportsConflictsWhenHigherPriorityPlanConsumesInventory()
    {
        var inventory = Enumerable.Range(1, 6)
            .Select(index => CreateMod($"shared-{index}", index, 1, 5))
            .ToList();
        var characters = new[]
        {
            new CharacterEntity { Id = "FIRST", Name = "First", Priority = 10 },
            new CharacterEntity { Id = "SECOND", Name = "Second", Priority = 1 }
        };
        var service = new ModAssignmentService(
            _context,
            NullLogger<ModAssignmentService>.Instance);

        var result = await service.CalculateRosterLoadoutsAsync(characters, inventory);

        Assert.False(result.IsComplete);
        Assert.Equal(6, result.Conflicts.Count);
        Assert.All(result.Conflicts, conflict => Assert.Equal("SECOND", conflict.CharacterId));
        Assert.Contains("priority-first", result.Status);
    }

    [Fact]
    public async Task CalculateRosterLoadoutsAsync_ReportsMissingSlotsWhenInventoryWasNeverAvailable()
    {
        var inventory = new[]
        {
            CreateMod("only-square", 1, 1, 6)
        };
        var characters = new[]
        {
            new CharacterEntity { Id = "ONLY", Name = "Only Character", Priority = 1 }
        };
        var service = new ModAssignmentService(
            _context,
            NullLogger<ModAssignmentService>.Instance);

        var result = await service.CalculateRosterLoadoutsAsync(characters, inventory);

        var plan = Assert.Single(result.Plans);
        Assert.Equal(1, plan.Loadout.Mods.Count);
        Assert.Equal(5, plan.Conflicts.Count);
        Assert.Contains(plan.Conflicts, conflict =>
            conflict.Slot == 2 && conflict.Reason.Contains("not available", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CalculateRosterLoadoutsAsync_ConsolidatesAvailableSwapCandidates()
    {
        var inventory = new List<GameModEntity>
        {
            CreateMod("current-square", 1, (int)ModSet.Health, 5),
            CreateMod("speed-arrow", 2, (int)ModSet.Speed, 6),
            CreateMod("speed-diamond", 3, (int)ModSet.Speed, 6),
            CreateMod("offense-triangle", 4, (int)ModSet.Offense, 6),
            CreateMod("offense-circle", 5, (int)ModSet.Offense, 6),
            CreateMod("health-cross", 6, (int)ModSet.Health, 5),
            CreateMod("higher-scoring-but-invalid-square", 1, (int)ModSet.Speed, 6)
        };
        foreach (var mod in inventory)
        {
            mod.CharacterId = "EQUIPPED";
        }

        var service = new ModAssignmentService(
            _context,
            NullLogger<ModAssignmentService>.Instance);

        var result = await service.CalculateRosterLoadoutsAsync(
            new[] { new CharacterEntity { Id = "FIRST", Name = "First", Priority = 10 } },
            inventory);

        var swap = Assert.Single(result.SwapRecommendations);
        Assert.Equal("FIRST", swap.CharacterId);
        Assert.Equal("higher-scoring-but-invalid-square", swap.CandidateModId);
        Assert.True(swap.CandidateAvailable);
        Assert.Contains("available", swap.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CalculateGloballyOptimizedRosterLoadoutsAsync_ChoosesNonOverlappingCompletePlans()
    {
        var inventory = Enumerable.Range(1, 12)
            .Select(index => CreateMod($"global-{index}", ((index - 1) % 6) + 1, index % 2 == 0 ? 1 : 2, 5))
            .ToList();
        var characters = new[]
        {
            new CharacterEntity { Id = "HIGH", Name = "Higher Priority", Priority = 10 },
            new CharacterEntity { Id = "LOW", Name = "Lower Priority", Priority = 1 }
        };
        var service = new ModAssignmentService(
            _context,
            NullLogger<ModAssignmentService>.Instance);

        var result = await service.CalculateGloballyOptimizedRosterLoadoutsAsync(characters, inventory);

        Assert.True(result.IsComplete);
        Assert.All(result.Plans, plan => Assert.Equal(6, plan.Loadout.Mods.Count));
        Assert.Equal(12, result.Plans.SelectMany(plan => plan.Loadout.Mods).Select(mod => mod.Id).Distinct().Count());
        Assert.Contains("global roster optimization", result.Status, StringComparison.OrdinalIgnoreCase);
    }

    private static GameModEntity CreateMod(string id, int slot, int set, int rarity)
    {
        return new GameModEntity
        {
            Id = id,
            PlayerAllyCode = "123456789",
            Slot = slot,
            Set = set,
            Rarity = rarity,
            Level = rarity == 6 ? 15 : 1,
            Tier = rarity == 6 ? 5 : 1
        };
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
