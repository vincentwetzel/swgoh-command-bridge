#nullable enable

using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using swgoh_command_bridge.UI;
using swgoh_command_bridge.UI.ViewModels;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task NavigationCommands_SelectEveryPrimaryScreenWithoutUsingUserCache()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var settings = new FakeSettingsService();
        using var composition = ApplicationComposition.CreateDefault(context, settings);
        var viewModel = new MainWindowViewModel(composition);

        Assert.Same(viewModel, viewModel.CurrentView);

        viewModel.GoToCharactersCommand.Execute(null);
        Assert.Same(viewModel.CharactersViewModel, viewModel.CurrentView);

        viewModel.GoToPrioritiesCommand.Execute(null);
        Assert.Same(viewModel.CharacterPrioritiesViewModel, viewModel.CurrentView);

        viewModel.GoToModsCommand.Execute(null);
        Assert.Same(viewModel.ModsViewModel, viewModel.CurrentView);

        viewModel.GoToOptimizerCommand.Execute(null);
        Assert.Same(viewModel.ModOptimizerViewModel, viewModel.CurrentView);

        viewModel.GoToThresholdsCommand.Execute(null);
        Assert.Same(viewModel.ModThresholdsViewModel, viewModel.CurrentView);

        viewModel.GoToSettingsCommand.Execute(null);
        Assert.Same(viewModel.SettingsViewModel, viewModel.CurrentView);

        await viewModel.GoToDiagnosticsCommand.ExecuteAsync(null);
        Assert.Same(viewModel.DiagnosticsViewModel, viewModel.CurrentView);

        viewModel.GoToHomeCommand.Execute(null);
        Assert.Same(viewModel, viewModel.CurrentView);
        Assert.Empty(settings.SavedSettings);
    }

    [Fact]
    public async Task InitializeAsync_ReportsActiveAccountCacheAndNextStep()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Players.Add(new PlayerEntity
        {
            AllyCode = "123456789",
            Name = "Cached Account"
        });
        context.Characters.Add(new CharacterEntity
        {
            Id = "CHARACTER",
            PlayerAllyCode = "123456789",
            Name = "Cached Character"
        });
        context.Mods.Add(new GameModEntity
        {
            Id = "MOD",
            PlayerAllyCode = "123456789",
            Slot = 1,
            Set = 1,
            Rarity = 5
        });
        await context.SaveChangesAsync();

        var settings = new FakeSettingsService(new AppSettings(DefaultAllyCode: "123456789"));
        using var composition = ApplicationComposition.CreateDefault(context, settings);
        var viewModel = new MainWindowViewModel(composition);

        await viewModel.InitializeAsync();

        Assert.Equal(1, viewModel.ActiveCharacterCount);
        Assert.Equal(1, viewModel.ActiveModCount);
        Assert.True(viewModel.HasActiveCache);
        Assert.Contains("Cached Account", viewModel.ActiveAccountSummaryText);
        Assert.Contains("inspect", viewModel.NextStepText, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public FakeSettingsService(AppSettings? currentSettings = null)
        {
            CurrentSettings = currentSettings ?? new AppSettings();
        }

        public AppSettings CurrentSettings { get; private set; }

        public string SettingsPath => "test-settings.json";

        public string DiagnosticsDirectory => "test-diagnostics";

        public System.Collections.Generic.List<AppSettings> SavedSettings { get; } = new();

        public Task LoadSettingsAsync() => Task.CompletedTask;

        public Task SaveSettingsAsync(AppSettings settings)
        {
            CurrentSettings = settings;
            SavedSettings.Add(settings);
            return Task.CompletedTask;
        }
    }
}
