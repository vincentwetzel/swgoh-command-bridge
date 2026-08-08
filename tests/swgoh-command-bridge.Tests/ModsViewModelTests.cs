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
        Assert.Equal("6-dot • Level 15 • Tier 5", filtered.QualitySummary);
        Assert.Equal("Health • Square", filtered.SetSlotSummary);
        Assert.Equal("Primary: +30 Speed", filtered.PrimaryStatSummary);
        Assert.Equal("+15 Speed (3)", filtered.SecondaryStatsSummary);
        Assert.Equal("Advisor threshold: Test", viewModel.ActiveThresholdText);
        Assert.Contains("1 of 3", viewModel.FilterSummaryText);

        viewModel.SelectedMod = filtered;
        Assert.Contains("6-dot Health mod", viewModel.SelectedModSummaryText);
        Assert.Equal("Set: Health", viewModel.SelectedModSetText);
        Assert.Equal("Slot: Square", viewModel.SelectedModSlotText);
        Assert.Contains("Speed", viewModel.SelectedSecondarySummaries);

        viewModel.ClearFiltersCommand.Execute(null);
        Assert.Equal(3, viewModel.FilteredMods.Count);
        Assert.False(viewModel.HasActiveFilters);
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
    public async Task SelectedModAnalysis_DoesNotAllowAnOlderSelectionToOverwriteTheCurrentOne()
    {
        await SeedModsAsync(2);
        var advisor = new StaleResultAdvisorService();
        var viewModel = new ModsViewModel(
            _context,
            advisor,
            activeAllyCodeProvider: () => "123456789");

        await viewModel.LoadModsAsync();
        var first = viewModel.FilteredMods[0];
        var second = viewModel.FilteredMods[1];
        viewModel.SelectedMod = first;
        await advisor.FirstAnalysisStarted.Task;

        var secondRecommendationApplied = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(viewModel.SelectedModRecommendation) &&
                viewModel.SelectedModRecommendation?.ModId == second.Id)
            {
                secondRecommendationApplied.TrySetResult(true);
            }
        };
        viewModel.SelectedMod = second;
        var secondRecommendation = await advisor.SecondAnalysisCompleted.Task;
        await secondRecommendationApplied.Task;

        Assert.Equal(second.Id, secondRecommendation.ModId);
        Assert.Equal(second.Id, viewModel.SelectedModRecommendation!.ModId);

        advisor.CompleteFirstAnalysis();
        await Task.Yield();
        Assert.Equal(second.Id, viewModel.SelectedModRecommendation.ModId);
    }

    [Fact]
    public async Task RefreshThresholdContext_ReanalyzesSelectedModWithTheCurrentThreshold()
    {
        await SeedModsAsync(1);
        var threshold = new ModUpgradeThreshold("first", "First", 5, 1, 10, true, 0);
        var advisor = new ThresholdRecordingAdvisorService();
        var viewModel = new ModsViewModel(
            _context,
            advisor,
            () => threshold,
            () => "123456789");

        await viewModel.LoadModsAsync();
        viewModel.SelectedMod = Assert.Single(viewModel.FilteredMods);
        await advisor.FirstCall.Task;

        threshold = new ModUpgradeThreshold("second", "Second", 6, 5, 25, true, 70);
        viewModel.RefreshThresholdContext();
        await advisor.SecondCall.Task;

        Assert.Equal(new[] { "first", "second" }, advisor.ThresholdIds);
        Assert.Equal("Advisor threshold: Second", viewModel.ActiveThresholdText);
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

    [Fact]
    public async Task Filters_CoverEveryInventoryCriterionAndResetCleanly()
    {
        await SeedFilterableModsAsync();
        var viewModel = new ModsViewModel(
            _context,
            new StubAdvisorService(),
            activeAllyCodeProvider: () => "123456789");

        await viewModel.LoadModsAsync();
        Assert.Equal(4, viewModel.FilteredMods.Count);

        viewModel.SearchText = "REY";
        Assert.Equal("alpha-equipped", Assert.Single(viewModel.FilteredMods).Id);
        viewModel.ClearFiltersCommand.Execute(null);

        viewModel.SlotFilter = 3;
        Assert.Equal("gamma-equipped", Assert.Single(viewModel.FilteredMods).Id);
        viewModel.ClearFiltersCommand.Execute(null);

        viewModel.SetFilter = 2;
        Assert.Equal(
            new[] { "beta-inventory", "gamma-equipped" },
            viewModel.FilteredMods.Select(mod => mod.Id));
        viewModel.ClearFiltersCommand.Execute(null);

        viewModel.PrimaryFilter = "Protection";
        Assert.Equal("gamma-equipped", Assert.Single(viewModel.FilteredMods).Id);
        viewModel.ClearFiltersCommand.Execute(null);

        viewModel.EquippedFilter = 1;
        Assert.Equal(
            new[] { "alpha-equipped", "gamma-equipped" },
            viewModel.FilteredMods.Select(mod => mod.Id));
        viewModel.ClearFiltersCommand.Execute(null);

        viewModel.EquippedFilter = 2;
        Assert.Equal(
            new[] { "beta-inventory", "delta-inventory" },
            viewModel.FilteredMods.Select(mod => mod.Id));
        viewModel.ClearFiltersCommand.Execute(null);

        viewModel.MinimumLevelFilter = "15";
        Assert.Equal("alpha-equipped", Assert.Single(viewModel.FilteredMods).Id);
        viewModel.ClearFiltersCommand.Execute(null);

        viewModel.TierFilter = "3";
        Assert.Equal("beta-inventory", Assert.Single(viewModel.FilteredMods).Id);
        viewModel.ClearFiltersCommand.Execute(null);

        viewModel.SecondaryFilter = "Speed>=15,Potency";
        Assert.Equal("alpha-equipped", Assert.Single(viewModel.FilteredMods).Id);
        Assert.False(viewModel.HasSecondaryFilterError);
        viewModel.ClearFiltersCommand.Execute(null);

        viewModel.SecondaryFilter = "Speed>>15";
        Assert.True(viewModel.HasSecondaryFilterError);
        viewModel.ClearFiltersCommand.Execute(null);

        viewModel.RarityFilter = 4;
        Assert.Equal("delta-inventory", Assert.Single(viewModel.FilteredMods).Id);
        Assert.Contains("1 of 4", viewModel.FilterSummaryText);
        viewModel.ClearFiltersCommand.Execute(null);

        Assert.Equal(4, viewModel.FilteredMods.Count);
        Assert.False(viewModel.HasActiveFilters);
    }

    [Fact]
    public async Task Sorting_UsesRequestedFieldAndDeterministicIdTieBreakers()
    {
        await SeedFilterableModsAsync();
        var viewModel = new ModsViewModel(
            _context,
            new StubAdvisorService(),
            activeAllyCodeProvider: () => "123456789");

        await viewModel.LoadModsAsync();

        viewModel.SortOption = "Level";
        Assert.Equal("alpha-equipped", viewModel.FilteredMods[0].Id);

        viewModel.SortOption = "Slot";
        Assert.Equal("alpha-equipped", viewModel.FilteredMods[0].Id);
        Assert.Equal("delta-inventory", viewModel.FilteredMods[^1].Id);

        viewModel.SortOption = "Primary";
        Assert.Equal("delta-inventory", viewModel.FilteredMods[0].Id);
    }

    [Fact]
    public async Task LoadModsAsync_SummarizesMultipleSecondariesWithoutHidingTheirCount()
    {
        await SeedFilterableModsAsync();
        var viewModel = new ModsViewModel(
            _context,
            new StubAdvisorService(),
            activeAllyCodeProvider: () => "123456789");

        await viewModel.LoadModsAsync();

        var mod = viewModel.FilteredMods.Single(item => item.Id == "alpha-equipped");
        Assert.Equal("+15 Speed (3) • +5.00% Potency", mod.SecondaryStatsSummary);
        Assert.Equal("Owner: Rey", $"Owner: {mod.OwnerDisplayName}");
    }

    [Fact]
    public async Task SelectedModAnalysis_UsesPersistedStatsAndExposesRuleAndEfficiencyExplanation()
    {
        var player = new PlayerEntity
        {
            AllyCode = "123456789",
            Name = "Advisor Account"
        };
        player.Mods.Add(new GameModEntity
        {
            Id = "advisor-mod",
            PlayerAllyCode = "123456789",
            Slot = 1,
            Set = (int)ModSet.Health,
            Level = 15,
            Tier = 5,
            Rarity = 5,
            PrimaryStatType = "Offense",
            PrimaryStatValue = 100,
            SecondaryStatsJson = "[{\"Type\":\"Speed\",\"Value\":20,\"RollCount\":1},{\"Type\":\"Potency\",\"Value\":5,\"RollCount\":1}]"
        });
        _context.Players.Add(player);
        await _context.SaveChangesAsync();

        var threshold = new ModUpgradeThreshold(
            "efficiency",
            "Efficiency",
            5,
            5,
            15,
            true,
            50);
        var viewModel = new ModsViewModel(
            _context,
            new ModAdvisorService(
                NullLogger<ModAdvisorService>.Instance,
                new ModMechanicsService()),
            () => threshold,
            () => "123456789");
        var recommendationReady = new TaskCompletionSource<ModRecommendation>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(viewModel.SelectedModRecommendation) &&
                viewModel.SelectedModRecommendation != null)
            {
                recommendationReady.TrySetResult(viewModel.SelectedModRecommendation);
            }
        };

        await viewModel.LoadModsAsync();
        viewModel.SelectedMod = Assert.Single(viewModel.FilteredMods);
        var recommendation = await recommendationReady.Task;

        Assert.Equal(ModRecommendationAction.Sell, recommendation.Action);
        Assert.Equal("efficiency", recommendation.ThresholdId);
        Assert.Contains("Rule: Efficiency (efficiency)", recommendation.RuleSummary);
        Assert.Equal(20, recommendation.CurrentEfficiency);
        Assert.Contains("projected maximum", recommendation.EfficiencySummary);
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

    private async Task SeedFilterableModsAsync()
    {
        var active = new PlayerEntity
        {
            AllyCode = "123456789",
            Name = "Filter Account",
            Characters =
            {
                new CharacterEntity
                {
                    Id = "REY",
                    PlayerAllyCode = "123456789",
                    Name = "Rey"
                }
            }
        };
        active.Mods.Add(new GameModEntity
        {
            Id = "alpha-equipped",
            PlayerAllyCode = "123456789",
            CharacterId = "REY",
            Slot = 1,
            Set = 1,
            Level = 15,
            Tier = 5,
            Rarity = 6,
            PrimaryStatType = "Speed",
            PrimaryStatValue = 30,
            SecondaryStatsJson = "[{\"Type\":\"Speed\",\"Value\":15,\"RollCount\":3},{\"Type\":\"Potency\",\"Value\":5,\"RollCount\":1}]"
        });
        active.Mods.Add(new GameModEntity
        {
            Id = "beta-inventory",
            PlayerAllyCode = "123456789",
            Slot = 2,
            Set = 2,
            Level = 10,
            Tier = 3,
            Rarity = 5,
            PrimaryStatType = "OffensePercent",
            PrimaryStatValue = 10,
            SecondaryStatsJson = "[{\"Type\":\"Potency\",\"Value\":5,\"RollCount\":1}]"
        });
        active.Mods.Add(new GameModEntity
        {
            Id = "gamma-equipped",
            PlayerAllyCode = "123456789",
            CharacterId = "REY",
            Slot = 3,
            Set = 2,
            Level = 12,
            Tier = 4,
            Rarity = 5,
            PrimaryStatType = "Protection",
            PrimaryStatValue = 20,
            SecondaryStatsJson = "[{\"Type\":\"Speed\",\"Value\":20,\"RollCount\":2}]"
        });
        active.Mods.Add(new GameModEntity
        {
            Id = "delta-inventory",
            PlayerAllyCode = "123456789",
            Slot = 4,
            Set = 3,
            Level = 8,
            Tier = 2,
            Rarity = 4,
            PrimaryStatType = "Health",
            PrimaryStatValue = 10
        });

        _context.Players.Add(active);
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

    private sealed class StaleResultAdvisorService : IModAdvisorService
    {
        public TaskCompletionSource<bool> FirstAnalysisStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<ModRecommendation> SecondAnalysisCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<ModRecommendation> _firstAnalysis =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ModRecommendation> AnalyzeModAsync(
            GameMod mod,
            ModUpgradeThreshold threshold,
            IEnumerable<Character> characters,
            CancellationToken cancellationToken = default)
        {
            if (mod.Id == "active-000")
            {
                FirstAnalysisStarted.TrySetResult(true);
                return _firstAnalysis.Task;
            }

            var recommendation = new ModRecommendation(
                mod.Id,
                ModRecommendationAction.Keep,
                "Current selection",
                20);
            SecondAnalysisCompleted.TrySetResult(recommendation);
            return Task.FromResult(recommendation);
        }

        public void CompleteFirstAnalysis() =>
            _firstAnalysis.TrySetResult(new ModRecommendation(
                "active-000",
                ModRecommendationAction.Sell,
                "Stale selection",
                1));
    }

    private sealed class ThresholdRecordingAdvisorService : IModAdvisorService
    {
        public List<string> ThresholdIds { get; } = new();

        public TaskCompletionSource<bool> FirstCall { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> SecondCall { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ModRecommendation> AnalyzeModAsync(
            GameMod mod,
            ModUpgradeThreshold threshold,
            IEnumerable<Character> characters,
            CancellationToken cancellationToken = default)
        {
            ThresholdIds.Add(threshold.Id);
            if (ThresholdIds.Count == 1)
            {
                FirstCall.TrySetResult(true);
            }
            else if (ThresholdIds.Count == 2)
            {
                SecondCall.TrySetResult(true);
            }

            return Task.FromResult(new ModRecommendation(
                mod.Id,
                ModRecommendationAction.Keep,
                threshold.Name,
                100));
        }
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
