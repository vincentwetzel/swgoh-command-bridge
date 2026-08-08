#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
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
            Name = "Cached Account",
            LastSyncedUtc = DateTime.UtcNow
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
        context.SyncHistory.Add(new SyncHistoryEntity
        {
            AllyCode = "123456789",
            StartedUtc = DateTime.UtcNow.AddMinutes(-1),
            CompletedUtc = DateTime.UtcNow,
            Status = "completed",
            CharacterCount = 1,
            ModCount = 1,
            WarningCount = 2
        });
        await context.SaveChangesAsync();

        var settings = new FakeSettingsService(new AppSettings(DefaultAllyCode: "123456789"));
        using var composition = ApplicationComposition.CreateDefault(context, settings);
        var viewModel = new MainWindowViewModel(composition);

        await viewModel.InitializeAsync();

        Assert.Equal(1, viewModel.ActiveCharacterCount);
        Assert.Equal(1, viewModel.ActiveModCount);
        Assert.True(viewModel.HasActiveCache);
        Assert.False(viewModel.IsActiveCacheStale);
        Assert.Contains("Cached Account", viewModel.ActiveAccountSummaryText);
        Assert.Contains("Last synced", viewModel.ActiveCacheFreshnessText);
        Assert.Contains("completed", viewModel.ActiveSyncOutcomeText);
        Assert.Contains("2 parser warning", viewModel.ActiveSyncOutcomeText);
        Assert.Contains("inspect", viewModel.NextStepText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncCommand_SuccessReportsCountsAndParserWarnings()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var settings = new FakeSettingsService();
        var playerService = new FakePlayerService(_ =>
            Task.FromResult(CreateProfile("123456789", warningCount: 1)));
        using var composition = ApplicationComposition.CreateDefault(context, settings, playerService);
        var viewModel = new MainWindowViewModel(composition)
        {
            AllyCode = "123456789"
        };

        await viewModel.SyncCommand.ExecuteAsync(null);

        Assert.Equal(OperationStatus.Success, viewModel.SyncState.Status);
        Assert.Contains("Completed sync for 123456789", viewModel.SyncSummaryText);
        Assert.Contains("0 characters, 0 mods, 1 parser warnings", viewModel.SyncSummaryText);
        Assert.True(viewModel.HasSyncSummary);
        Assert.False(viewModel.CanRetrySync);
    }

    [Fact]
    public async Task SyncCommand_FailurePreservesCacheAndExposesRetryableSummary()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var playerService = new FakePlayerService(_ =>
            Task.FromException<PlayerProfile>(new InvalidOperationException("synthetic failure")));
        using var composition = ApplicationComposition.CreateDefault(
            context,
            new FakeSettingsService(),
            playerService);
        var viewModel = new MainWindowViewModel(composition)
        {
            AllyCode = "123456789"
        };

        await viewModel.SyncCommand.ExecuteAsync(null);

        Assert.Equal(OperationStatus.Error, viewModel.SyncState.Status);
        Assert.Contains("existing cached data was preserved", viewModel.SyncSummaryText);
        Assert.Contains("retry", viewModel.SyncSummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.True(viewModel.CanRetrySync);
    }

    [Fact]
    public async Task CancelSyncCommandReportsCancellationAndOffersRetry()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var playerService = new FakePlayerService(async cancellationToken =>
        {
            started.SetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return CreateProfile("123456789");
        });
        using var composition = ApplicationComposition.CreateDefault(
            context,
            new FakeSettingsService(),
            playerService);
        var viewModel = new MainWindowViewModel(composition)
        {
            AllyCode = "123456789"
        };

        var syncTask = viewModel.SyncCommand.ExecuteAsync(null);
        await started.Task;
        viewModel.CancelSyncCommand.Execute(null);
        await syncTask;

        Assert.Equal(OperationStatus.Error, viewModel.SyncState.Status);
        Assert.Contains("Cancelled sync for 123456789", viewModel.SyncSummaryText);
        Assert.Contains("preserved", viewModel.SyncSummaryText);
        Assert.True(viewModel.CanRetrySync);
    }

    private static PlayerProfile CreateProfile(string allyCode, int warningCount = 0)
    {
        var warnings = new List<string>();
        for (var index = 0; index < warningCount; index++)
        {
            warnings.Add($"synthetic warning {index + 1}");
        }

        return new PlayerProfile(
            allyCode,
            "Test Account",
            85,
            1_000_000,
            Array.Empty<Character>(),
            Array.Empty<GameMod>())
        {
            Diagnostics = new PlayerSyncDiagnostics(0, 0, 0, 0, 0, warnings)
        };
    }

    private sealed class FakePlayerService : IPlayerService
    {
        private readonly Func<CancellationToken, Task<PlayerProfile>> _sync;

        public FakePlayerService(Func<CancellationToken, Task<PlayerProfile>> sync)
        {
            _sync = sync;
        }

        public Task<PlayerProfile> GetPlayerProfileAsync(
            string allyCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateProfile(allyCode));

        public Task<PlayerProfile> SyncPlayerProfileAsync(
            string allyCode,
            CancellationToken cancellationToken = default,
            IProgress<PlayerSyncProgress>? progress = null) =>
            _sync(cancellationToken);
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
