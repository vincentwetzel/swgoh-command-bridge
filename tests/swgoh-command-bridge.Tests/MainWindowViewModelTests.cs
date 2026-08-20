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
    public async Task NavigationCommands_SelectEveryWorkspaceAndUtilityScreenWithoutUsingUserCache()
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
        Assert.Equal("Roster · Characters", viewModel.CharactersViewModel.HeaderText);

        viewModel.GoToPrioritiesCommand.Execute(null);
        Assert.Same(viewModel.CharacterPrioritiesViewModel, viewModel.CurrentView);
        Assert.Equal("Roster · Character Priorities", viewModel.CharacterPrioritiesViewModel.HeaderText);

        viewModel.GoToModsCommand.Execute(null);
        Assert.Same(viewModel.ModsViewModel, viewModel.CurrentView);
        Assert.Equal("Mods · Inventory", viewModel.ModsViewModel.HeaderText);

        viewModel.GoToOptimizerCommand.Execute(null);
        Assert.Same(viewModel.ModOptimizerViewModel, viewModel.CurrentView);
        Assert.Equal("Optimize · Mod Assignments", viewModel.ModOptimizerViewModel.HeaderText);

        viewModel.GoToThresholdsCommand.Execute(null);
        Assert.Same(viewModel.ModThresholdsViewModel, viewModel.CurrentView);
        Assert.Equal("Mods · Upgrade Rules", viewModel.ModThresholdsViewModel.HeaderText);

        viewModel.GoToSettingsCommand.Execute(null);
        Assert.Same(viewModel.SettingsViewModel, viewModel.CurrentView);

        await viewModel.GoToDiagnosticsCommand.ExecuteAsync(null);
        Assert.Same(viewModel.DiagnosticsViewModel, viewModel.CurrentView);

        viewModel.GoToHomeCommand.Execute(null);
        Assert.Same(viewModel, viewModel.CurrentView);
        Assert.Empty(settings.SavedSettings);
    }

    [Fact]
    public async Task PriorityChange_RefreshesRosterAndOptimizerProjections()
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
            Name = "Active Account",
            Characters =
            {
                new CharacterEntity
                {
                    Id = "FIRST",
                    PlayerAllyCode = "123456789",
                    Name = "First Character"
                },
                new CharacterEntity
                {
                    Id = "SECOND",
                    PlayerAllyCode = "123456789",
                    Name = "Second Character"
                }
            }
        });
        await context.SaveChangesAsync();

        using var composition = ApplicationComposition.CreateDefault(
            context,
            new FakeSettingsService(new AppSettings(DefaultAllyCode: "123456789")));
        var viewModel = new MainWindowViewModel(composition);
        await viewModel.CharactersViewModel.LoadCharactersAsync();
        await viewModel.CharacterPrioritiesViewModel.LoadCharactersAsync();
        await viewModel.ModOptimizerViewModel.LoadCharactersAsync();
        viewModel.ModOptimizerViewModel.SelectedCharacter = viewModel.ModOptimizerViewModel.Characters
            .Single(character => character.Id == "SECOND");

        var secondCharacter = viewModel.CharacterPrioritiesViewModel.Characters
            .Single(character => character.Id == "SECOND");
        await viewModel.CharacterPrioritiesViewModel.MoveCharacterAsync(
            secondCharacter,
            viewModel.CharacterPrioritiesViewModel.RankedTiers[0],
            0);

        Assert.Equal("SECOND", viewModel.CharactersViewModel.Characters[0].Id);
        Assert.Equal(100_000, viewModel.CharactersViewModel.Characters[0].Priority);
        Assert.Equal("SECOND", viewModel.ModOptimizerViewModel.Characters[0].Id);
        Assert.Equal(100_000, viewModel.ModOptimizerViewModel.Characters[0].Priority);
        Assert.Equal("SECOND", viewModel.ModOptimizerViewModel.SelectedCharacter?.Id);
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
    public async Task AccountSearch_FiltersCachedAccountsByNameOrAllyCodeAndRecoversOnClear()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Players.AddRange(
            new PlayerEntity { AllyCode = "123456789", Name = "Alpha Account" },
            new PlayerEntity { AllyCode = "987654321", Name = "Beta Account" },
            new PlayerEntity { AllyCode = "111222333", Name = "Gamma Account" });
        await context.SaveChangesAsync();

        using var composition = ApplicationComposition.CreateDefault(context, new FakeSettingsService());
        var viewModel = new MainWindowViewModel(composition);

        await viewModel.InitializeAsync();
        Assert.Equal(3, viewModel.VisibleCachedAccounts.Count);

        viewModel.AccountSearchText = "beta";
        Assert.Equal("987654321", Assert.Single(viewModel.VisibleCachedAccounts).AllyCode);
        Assert.Contains("1 of 3", viewModel.CachedAccountFilterStatusText);

        viewModel.AccountSearchText = "111222";
        Assert.Equal("Gamma Account", Assert.Single(viewModel.VisibleCachedAccounts).Name);

        viewModel.AccountSearchText = "no match";
        Assert.Empty(viewModel.VisibleCachedAccounts);
        Assert.True(viewModel.HasNoVisibleCachedAccounts);

        viewModel.AccountSearchText = string.Empty;
        Assert.Equal(3, viewModel.VisibleCachedAccounts.Count);
        Assert.False(viewModel.HasNoVisibleCachedAccounts);
    }

    [Fact]
    public async Task SwitchAccountCommand_ActivatesCachedAccountAndPersistsSelection()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Players.AddRange(
            new PlayerEntity
            {
                AllyCode = "123456789",
                Name = "Alpha Account",
                LastSyncedUtc = DateTime.UtcNow
            },
            new PlayerEntity
            {
                AllyCode = "987654321",
                Name = "Beta Account",
                LastSyncedUtc = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        var settings = new FakeSettingsService(new AppSettings(DefaultAllyCode: "123456789"));
        using var composition = ApplicationComposition.CreateDefault(context, settings);
        var viewModel = new MainWindowViewModel(composition);
        await viewModel.InitializeAsync();

        var beta = Assert.Single(viewModel.CachedAccounts, account => account.AllyCode == "987654321");
        await viewModel.SwitchAccountCommand.ExecuteAsync(beta);

        Assert.Equal("987654321", viewModel.AllyCode);
        Assert.Equal("Beta Account", viewModel.ActiveAccountDisplayName);
        Assert.Equal("987654321", settings.CurrentSettings.DefaultAllyCode);
    }

    [Fact]
    public async Task AddAccountCommand_ClearsActiveSelectionAndOpensHomeSyncFlow()
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
            Name = "Existing Account",
            LastSyncedUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        using var composition = ApplicationComposition.CreateDefault(
            context,
            new FakeSettingsService(new AppSettings(DefaultAllyCode: "123456789")));
        var viewModel = new MainWindowViewModel(composition);
        await viewModel.InitializeAsync();

        await viewModel.AddAccountCommand.ExecuteAsync(null);

        Assert.Same(viewModel, viewModel.CurrentView);
        Assert.Empty(viewModel.AllyCode);
        Assert.Null(viewModel.SelectedCachedAccount);
        Assert.Equal("Select account", viewModel.ActiveAccountDisplayName);
        Assert.Contains("new account", viewModel.AccountManagementStatusText);
    }

    [Fact]
    public async Task InitializeAsync_RefreshesStaleActiveAccountInBackground()
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
            Name = "Stale Account",
            LastSyncedUtc = DateTime.UtcNow.AddDays(-2)
        });
        await context.SaveChangesAsync();

        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var playerService = new FakePlayerService(async cancellationToken =>
        {
            started.SetResult(true);
            await release.Task.WaitAsync(cancellationToken);
            return CreateProfile("123456789");
        });
        using var composition = ApplicationComposition.CreateDefault(
            context,
            new FakeSettingsService(new AppSettings(DefaultAllyCode: "123456789")),
            playerService);
        var viewModel = new MainWindowViewModel(composition);

        await viewModel.InitializeAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(viewModel.IsSyncing);
        Assert.Contains("Syncing", viewModel.SyncStatusText);

        release.SetResult(true);
        for (var attempt = 0; attempt < 100 && viewModel.IsSyncing; attempt++)
        {
            await Task.Delay(10);
        }

        Assert.Equal(OperationStatus.Success, viewModel.SyncState.Status);
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
