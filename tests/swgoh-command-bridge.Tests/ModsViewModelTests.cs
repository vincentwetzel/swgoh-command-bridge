#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using swgoh_command_bridge.UI.ViewModels;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class ModsViewModelTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    public ModsViewModelTests()
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
    public async Task LoadModsAsync_ScopesInventoryAndAppliesStructuredFilters()
    {
        await SeedModsAsync(3);
        var viewModel = new ModsViewModel(
            _context,
            new StubAdvisorService(),
            () => new ModUpgradeThreshold("test", "Test", 5, 1, 10, true, 0),
            () => "123456789");

        await viewModel.LoadModsAsync();

        Assert.Equal(OperationStatus.Success, viewModel.State.Status);
        Assert.Equal(3, viewModel.FilteredMods.Count);
        Assert.All(viewModel.FilteredMods, mod => Assert.Equal("123456789", mod.PlayerAllyCode));

        viewModel.RarityFilter = 6;
        viewModel.SecondaryFilter = "Speed>=15";

        var filtered = Assert.Single(viewModel.FilteredMods);
        Assert.Equal("active-000", filtered.Id);
        Assert.Equal("Un-equipped", filtered.OwnerDisplayName);
        Assert.Equal("Advisor threshold: Test", viewModel.ActiveThresholdText);
    }

    [Fact]
    public async Task LoadModsAsync_PaginatesDeterministically()
    {
        await SeedModsAsync(105);
        var viewModel = new ModsViewModel(
            _context,
            new StubAdvisorService(),
            activeAllyCodeProvider: () => "123456789");

        await viewModel.LoadModsAsync();

        Assert.Equal(2, viewModel.PageCount);
        Assert.Equal(100, viewModel.FilteredMods.Count);
        Assert.Equal("active-000", viewModel.FilteredMods[0].Id);
        Assert.True(viewModel.CanNextPage);

        viewModel.NextPageCommand.Execute(null);

        Assert.Equal(2, viewModel.CurrentPage);
        Assert.Single(viewModel.FilteredMods);
        Assert.Equal("active-104", viewModel.FilteredMods[0].Id);
        Assert.False(viewModel.CanNextPage);
    }

    [Fact]
    public async Task LoadModsAsync_WithEmptyActiveScopeDoesNotShowOtherInventory()
    {
        await SeedModsAsync(2);
        var viewModel = new ModsViewModel(
            _context,
            new StubAdvisorService(),
            activeAllyCodeProvider: () => string.Empty);

        await viewModel.LoadModsAsync();

        Assert.Empty(viewModel.FilteredMods);
        Assert.Equal(OperationStatus.Empty, viewModel.State.Status);
    }

    private async Task SeedModsAsync(int activeCount)
    {
        var active = new PlayerEntity
        {
            AllyCode = "123456789",
            Name = "Active",
            Characters =
            {
                new CharacterEntity
                {
                    Id = "ACTIVE",
                    PlayerAllyCode = "123456789",
                    Name = "Active Character"
                }
            }
        };
        for (var index = 0; index < activeCount; index++)
        {
            active.Mods.Add(CreateMod($"active-{index:000}", "123456789", index));
        }

        var other = new PlayerEntity
        {
            AllyCode = "987654321",
            Name = "Other"
        };
        other.Mods.Add(CreateMod("other-000", "987654321", 0));
        _context.Players.AddRange(active, other);
        await _context.SaveChangesAsync();
    }

    private static GameModEntity CreateMod(string id, string allyCode, int index) =>
        new()
        {
            Id = id,
            PlayerAllyCode = allyCode,
            Slot = (index % 6) + 1,
            Set = 1,
            Level = 15,
            Tier = 5,
            Rarity = index == 0 ? 6 : 5,
            PrimaryStatType = index == 0 ? "Speed" : "OffensePercent",
            PrimaryStatValue = index == 0 ? 30 : 10,
            SecondaryStatsJson = index == 0
                ? "[{\"Type\":\"Speed\",\"Value\":15,\"RollCount\":3}]"
                : "[]"
        };

    private sealed class StubAdvisorService : IModAdvisorService
    {
        public Task<ModRecommendation> AnalyzeModAsync(
            GameMod mod,
            ModUpgradeThreshold threshold,
            IEnumerable<Character> characters,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ModRecommendation(
                mod.Id,
                ModRecommendationAction.Keep,
                "Test recommendation",
                100));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
