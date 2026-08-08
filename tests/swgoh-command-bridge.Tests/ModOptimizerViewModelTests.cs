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
using swgoh_command_bridge.UI.ViewModels;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class ModOptimizerViewModelTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    public ModOptimizerViewModelTests()
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
    public async Task RefreshAsync_LoadsCurrentRecommendationAndCalculatesLoadout()
    {
        await SeedOptimizerDataAsync(DateTime.UtcNow.AddHours(-1));
        var viewModel = CreateViewModel();

        await viewModel.LoadCharactersAsync();
        viewModel.SelectedCharacter = Assert.Single(viewModel.Characters);
        await viewModel.RefreshAsync();

        Assert.Equal(OperationStatus.Success, viewModel.State.Status);
        Assert.Equal(6, viewModel.RecommendedLoadout.Count);
        Assert.False(viewModel.IsRecommendationStale);
        Assert.Contains("Current fixture-source", viewModel.RecommendationStatusText);
        Assert.Contains("Speed", viewModel.TargetSets[0]);
        Assert.Contains("fixture-source", viewModel.RecommendationSourceUrlText);
        Assert.NotEmpty(viewModel.LoadoutExplanations);
        Assert.Contains("Combined assignment score", viewModel.LoadoutScoreText);
        Assert.Contains(viewModel.AlternativeSummaries, summary => summary.Contains("optimizer-alt"));
    }

    [Fact]
    public async Task RefreshAsync_LabelsStaleRecommendationData()
    {
        await SeedOptimizerDataAsync(DateTime.UtcNow.AddDays(-8));
        var viewModel = CreateViewModel();

        await viewModel.LoadCharactersAsync();
        viewModel.SelectedCharacter = Assert.Single(viewModel.Characters);
        await viewModel.RefreshAsync();

        Assert.True(viewModel.IsRecommendationStale);
        Assert.Contains("Stale", viewModel.RecommendationStatusText);
        Assert.Contains("Refresh recommendations", viewModel.RecommendationStatusText);
    }

    [Fact]
    public async Task CalculateRosterCommand_ReportsPriorityOrderedPlans()
    {
        await SeedOptimizerDataAsync(DateTime.UtcNow);
        _context.Characters.Add(new CharacterEntity
        {
            Id = "SECOND",
            PlayerAllyCode = "123456789",
            Name = "Second Character",
            Priority = 1
        });
        await _context.SaveChangesAsync();
        var viewModel = CreateViewModel();

        await viewModel.CalculateRosterCommand.ExecuteAsync(null);

        Assert.NotEmpty(viewModel.RosterPlanSummaries);
        Assert.Contains("FIRST", string.Join(Environment.NewLine, viewModel.RosterPlanSummaries));
        Assert.Contains("priority-first", viewModel.RosterPlanStatusText);
    }

    [Fact]
    public async Task OptimizeRosterCommand_ReportsBoundedGlobalPlan()
    {
        await SeedOptimizerDataAsync(DateTime.UtcNow);
        var viewModel = CreateViewModel();

        await viewModel.OptimizeRosterCommand.ExecuteAsync(null);

        Assert.NotEmpty(viewModel.RosterPlanSummaries);
        Assert.Contains("global roster optimization", viewModel.RosterPlanStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadCharactersAsync_WithEmptyActiveScopeProducesEmptyState()
    {
        await SeedOptimizerDataAsync(DateTime.UtcNow);
        var viewModel = new ModOptimizerViewModel(
            _context,
            new ModAssignmentService(_context, NullLogger<ModAssignmentService>.Instance),
            null,
            activeAllyCodeProvider: () => string.Empty);

        await viewModel.LoadCharactersAsync();

        Assert.Empty(viewModel.Characters);
        Assert.Equal(OperationStatus.Empty, viewModel.State.Status);
        Assert.True(viewModel.IsEmpty);
    }

    private ModOptimizerViewModel CreateViewModel() =>
        new(
            _context,
            new ModAssignmentService(_context, NullLogger<ModAssignmentService>.Instance),
            null,
            activeAllyCodeProvider: () => "123456789");

    private async Task SeedOptimizerDataAsync(DateTime recommendationTime)
    {
        var player = new PlayerEntity
        {
            AllyCode = "123456789",
            Name = "Active Player",
            Characters =
            {
                new CharacterEntity
                {
                    Id = "FIRST",
                    PlayerAllyCode = "123456789",
                    Name = "First Character",
                    Priority = 10
                }
            }
        };
        for (var index = 0; index < 6; index++)
        {
            player.Mods.Add(new GameModEntity
            {
                Id = $"optimizer-{index}",
                PlayerAllyCode = "123456789",
                Slot = index + 1,
                Set = index < 2 ? (int)ModSet.Health : (int)ModSet.Speed,
                Rarity = 6,
                Level = 15,
                Tier = 5,
                PrimaryStatType = index == 0 ? "Speed" : "OffensePercent",
                PrimaryStatValue = index == 0 ? 30 : 10
            });
        }
        player.Mods.Add(new GameModEntity
        {
            Id = "optimizer-alt",
            PlayerAllyCode = "123456789",
            Slot = 1,
            Set = (int)ModSet.Speed,
            Rarity = 5,
            Level = 15,
            Tier = 5,
            PrimaryStatType = "Speed",
            PrimaryStatValue = 30
        });

        _context.Players.Add(player);
        _context.SwgohGgRecommendations.Add(new SwgohGgRecommendationEntity
        {
            CharacterId = "FIRST",
            PlayerAllyCode = "123456789",
            Source = "fixture-source",
            SourceUrl = "https://fixture.test/first",
            LastUpdatedUtc = recommendationTime,
            PopularityPercentage = 82.5,
            SetRecommendationsJson = "[{\"name\":\"Speed\",\"percentage\":80}]",
            PrimaryStatsJson = "{\"Slot 1\":[{\"statName\":\"Speed\",\"percentage\":95}]}"
        });
        await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
