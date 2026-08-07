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
