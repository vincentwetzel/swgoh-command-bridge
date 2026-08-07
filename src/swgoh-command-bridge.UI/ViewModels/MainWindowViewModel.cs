using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using Microsoft.EntityFrameworkCore;
using swgoh_command_bridge.UI;

namespace swgoh_command_bridge.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ApplicationComposition _composition;
    private readonly AppDbContext _context;
    private readonly ISettingsService _settingsService;
    private readonly IPlayerService _playerService;
    private readonly HttpClient _comlinkClient;
    private CancellationTokenSource? _syncCancellation;
    private string _lastSyncAllyCode = string.Empty;
    private string _allyCode = string.Empty;
    private PlayerEntity? _selectedCachedAccount;
    private string _syncStatusOverride = string.Empty;
    private string _syncProgressText = string.Empty;
    private string _accountManagementStatusText = string.Empty;
    private string _activeAccountSummaryText = "No active account cache is selected.";
    private string _nextStepText = "Enter a nine-digit ally code or choose a cached account to begin.";
    private int _activeCharacterCount;
    private int _activeModCount;
    private bool _confirmAccountRemoval;
    private OperationState<bool> _startupState = OperationState<bool>.ToSuccess(true);
    private OperationState<PlayerProfile> _syncState = OperationState<PlayerProfile>.ToEmpty();

    [ObservableProperty]
    private ViewModelBase _currentView;

    public MainWindowViewModel() : this(ApplicationComposition.CreateDefault())
    {
    }

    public MainWindowViewModel(AppDbContext context)
        : this(ApplicationComposition.CreateDefault(context))
    {
    }

    public MainWindowViewModel(ApplicationComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        _composition = composition;
        _context = composition.Database;
        try
        {
            _context.InitializeDatabase();
            _composition.EventLog.Info("startup", "Local cache initialized successfully.");
        }
        catch (Exception ex)
        {
            _composition.EventLog.Error("startup", "Local cache initialization failed.");
            StartupState = OperationState<bool>.ToError(
                $"Local cache initialization failed: {ex.Message}");
        }

        _settingsService = composition.Settings;
        AllyCode = _settingsService.CurrentSettings.DefaultAllyCode ?? string.Empty;

        _comlinkClient = composition.ComlinkClient;
        _playerService = composition.PlayerService;

        CharactersViewModel = new CharactersViewModel(_context, () => AllyCode);
        CharacterPrioritiesViewModel = new CharacterPrioritiesViewModel(_context, () => AllyCode);
        ModThresholdsViewModel = new ModThresholdsViewModel(_settingsService);
        ModsViewModel = new ModsViewModel(
            _context,
            composition.AdvisorService,
            () => ModThresholdsViewModel.SelectedThreshold,
            () => AllyCode);
        ModOptimizerViewModel = new ModOptimizerViewModel(
            _context,
            composition.AssignmentService,
            composition.ScraperService,
            () => AllyCode,
            _settingsService);
        DiagnosticsViewModel = new DiagnosticsViewModel(
            _context,
            _settingsService,
            () => AllyCode,
            composition.EventLog);
        ModThresholdsViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ModThresholdsViewModel.SelectedThreshold))
            {
                ModsViewModel.RefreshThresholdContext();
            }
        };
        UseCachedAccountCommand = new AsyncRelayCommand(
            UseCachedAccountAsync,
            () => SelectedCachedAccount != null && !IsSyncing);
        RemoveCachedAccountCommand = new AsyncRelayCommand(
            RemoveCachedAccountAsync,
            CanRemoveCachedAccount);
        SettingsViewModel = new SettingsViewModel(
            _settingsService,
            ApplyComlinkUrl,
            allyCode => AllyCode = allyCode,
            TestComlinkAsync,
            ResetCacheAsync,
            BackupCacheAsync,
            RestoreCacheAsync,
            RefreshAfterSettingsImportAsync,
            composition.EventLog);
        _currentView = this;
    }

    public ObservableCollection<PlayerEntity> CachedAccounts { get; } = new();

    public bool HasCachedAccounts => CachedAccounts.Count > 0;

    public IAsyncRelayCommand UseCachedAccountCommand { get; }

    public IAsyncRelayCommand RemoveCachedAccountCommand { get; }

    public bool ConfirmAccountRemoval
    {
        get => _confirmAccountRemoval;
        set
        {
            if (_confirmAccountRemoval == value)
            {
                return;
            }

            _confirmAccountRemoval = value;
            OnPropertyChanged(nameof(ConfirmAccountRemoval));
            RemoveCachedAccountCommand.NotifyCanExecuteChanged();
        }
    }

    public string AccountManagementStatusText
    {
        get => _accountManagementStatusText;
        private set
        {
            if (_accountManagementStatusText == value)
            {
                return;
            }

            _accountManagementStatusText = value;
            OnPropertyChanged(nameof(AccountManagementStatusText));
        }
    }

    public string ActiveAccountSummaryText
    {
        get => _activeAccountSummaryText;
        private set
        {
            if (_activeAccountSummaryText == value)
            {
                return;
            }

            _activeAccountSummaryText = value;
            OnPropertyChanged(nameof(ActiveAccountSummaryText));
        }
    }

    public string NextStepText
    {
        get => _nextStepText;
        private set
        {
            if (_nextStepText == value)
            {
                return;
            }

            _nextStepText = value;
            OnPropertyChanged(nameof(NextStepText));
        }
    }

    public int ActiveCharacterCount
    {
        get => _activeCharacterCount;
        private set
        {
            if (_activeCharacterCount == value)
            {
                return;
            }

            _activeCharacterCount = value;
            OnPropertyChanged(nameof(ActiveCharacterCount));
            OnPropertyChanged(nameof(HasActiveCache));
        }
    }

    public int ActiveModCount
    {
        get => _activeModCount;
        private set
        {
            if (_activeModCount == value)
            {
                return;
            }

            _activeModCount = value;
            OnPropertyChanged(nameof(ActiveModCount));
            OnPropertyChanged(nameof(HasActiveCache));
        }
    }

    public bool HasActiveCache => ActiveCharacterCount > 0 || ActiveModCount > 0;

    public async Task InitializeAsync()
    {
        if (HasStartupError)
        {
            return;
        }

        await LoadCachedAccountsAsync();
        await LoadFeatureDataAsync();
    }

    public OperationState<bool> StartupState
    {
        get => _startupState;
        private set
        {
            _startupState = value;
            OnPropertyChanged(nameof(StartupState));
            OnPropertyChanged(nameof(HasStartupError));
            OnPropertyChanged(nameof(StartupErrorMessage));
            RetryStartupCommand.NotifyCanExecuteChanged();
        }
    }

    public bool HasStartupError => StartupState.Status == OperationStatus.Error;

    public string StartupErrorMessage => StartupState.ErrorMessage ?? string.Empty;

    public string Greeting => "SWGOH Command Bridge";

    public string AllyCode
    {
        get => _allyCode;
        set
        {
            if (_allyCode == value)
            {
                return;
            }

            _allyCode = value;
            if (!string.Equals(value.Trim(), _selectedCachedAccount?.AllyCode, StringComparison.Ordinal))
            {
                _syncStatusOverride = string.Empty;
            }
            OnPropertyChanged(nameof(AllyCode));
            OnPropertyChanged(nameof(AllyCodeValidationMessage));
            OnPropertyChanged(nameof(HasAllyCodeValidationError));
            OnPropertyChanged(nameof(SyncStatusText));
            SyncCommand.NotifyCanExecuteChanged();
            UseCachedAccountCommand?.NotifyCanExecuteChanged();
        }
    }

    public PlayerEntity? SelectedCachedAccount
    {
        get => _selectedCachedAccount;
        set
        {
            if (_selectedCachedAccount == value)
            {
                return;
            }

            _selectedCachedAccount = value;
            OnPropertyChanged(nameof(SelectedCachedAccount));
            UseCachedAccountCommand?.NotifyCanExecuteChanged();
            RemoveCachedAccountCommand?.NotifyCanExecuteChanged();
        }
    }

    public OperationState<PlayerProfile> SyncState
    {
        get => _syncState;
        private set
        {
            _syncState = value;
            OnPropertyChanged(nameof(SyncState));
            OnPropertyChanged(nameof(IsSyncing));
            OnPropertyChanged(nameof(CanCancelSync));
            OnPropertyChanged(nameof(CanRetrySync));
            OnPropertyChanged(nameof(SyncStatusText));
            OnPropertyChanged(nameof(SyncDiagnosticsText));
            OnPropertyChanged(nameof(HasSyncDiagnostics));
            SyncCommand.NotifyCanExecuteChanged();
            RetrySyncCommand.NotifyCanExecuteChanged();
            UseCachedAccountCommand?.NotifyCanExecuteChanged();
            RemoveCachedAccountCommand?.NotifyCanExecuteChanged();
        }
    }

    public bool IsSyncing => SyncState.Status == OperationStatus.Loading;

    public string AllyCodeValidationMessage =>
        AllyCodeValidator.TryNormalize(AllyCode, out _, out var message)
            ? string.Empty
            : message;

    public bool HasAllyCodeValidationError => !string.IsNullOrWhiteSpace(AllyCodeValidationMessage);

    public string SyncDiagnosticsText => SyncState.Data?.Diagnostics.Summary ?? string.Empty;

    public bool HasSyncDiagnostics => SyncState.Data?.Diagnostics.HasWarnings == true;

    public string SyncProgressText
    {
        get => _syncProgressText;
        private set
        {
            if (_syncProgressText == value)
            {
                return;
            }

            _syncProgressText = value;
            OnPropertyChanged(nameof(SyncProgressText));
            OnPropertyChanged(nameof(HasSyncProgress));
        }
    }

    public bool HasSyncProgress => !string.IsNullOrWhiteSpace(SyncProgressText);

    public string SyncStatusText => SyncState.Status switch
    {
        _ when !string.IsNullOrWhiteSpace(_syncStatusOverride) => _syncStatusOverride,
        OperationStatus.Loading => "Syncing account data...",
        OperationStatus.Success when SyncState.Data != null =>
            $"Synced {SyncState.Data.Characters.Count} characters and {SyncState.Data.Mods.Count} mods.",
        OperationStatus.Error => SyncState.ErrorMessage ?? "Sync failed.",
        _ when HasCachedAccounts && string.IsNullOrWhiteSpace(AllyCode) =>
            "Select a cached account to work offline, or enter a nine-digit ally code to sync.",
        _ => "Enter an ally code to sync your account."
    };

    public CharactersViewModel CharactersViewModel { get; }

    public CharacterPrioritiesViewModel CharacterPrioritiesViewModel { get; }

    public ModsViewModel ModsViewModel { get; }

    public ModOptimizerViewModel ModOptimizerViewModel { get; }

    public ModThresholdsViewModel ModThresholdsViewModel { get; }

    public SettingsViewModel SettingsViewModel { get; }

    public DiagnosticsViewModel DiagnosticsViewModel { get; }

    [RelayCommand(CanExecute = nameof(CanSync))]
    private async Task SyncAsync()
    {
        if (!AllyCodeValidator.TryNormalize(AllyCode, out var allyCode, out var validationMessage))
        {
            SyncState = OperationState<PlayerProfile>.ToError(validationMessage);
            return;
        }

        if (!string.Equals(AllyCode, allyCode, StringComparison.Ordinal))
        {
            AllyCode = allyCode;
        }

        _lastSyncAllyCode = allyCode;
        _composition.EventLog.Info("account-sync", "Account sync started.");
        _syncStatusOverride = string.Empty;
        SyncProgressText = "Connecting to Comlink...";
        OnPropertyChanged(nameof(SyncStatusText));
        _syncCancellation?.Dispose();
        _syncCancellation = new CancellationTokenSource();
        SyncState = OperationState<PlayerProfile>.ToLoading();

        try
        {
            var profile = await _playerService.SyncPlayerProfileAsync(
                allyCode,
                _syncCancellation.Token,
                new Progress<PlayerSyncProgress>(update => SyncProgressText = update.Message));
            await _settingsService.SaveSettingsAsync(
                _settingsService.CurrentSettings with { DefaultAllyCode = allyCode });

            await LoadCachedAccountsAsync();
            await CharactersViewModel.LoadCharactersAsync();
            await CharacterPrioritiesViewModel.LoadCharactersAsync();
            await ModsViewModel.LoadModsAsync();
            await ModOptimizerViewModel.LoadCharactersAsync();
            await DiagnosticsViewModel.RefreshAsync();
            SyncState = OperationState<PlayerProfile>.ToSuccess(profile);
            SyncProgressText = "Account cache saved successfully.";
            _composition.EventLog.Info(
                "account-sync",
                $"Account sync completed with {profile.Characters.Count} characters and {profile.Mods.Count} mods.");
        }
        catch (OperationCanceledException)
        {
            _composition.EventLog.Warning("account-sync", "Account sync was cancelled.");
            SyncProgressText = "Account sync cancelled.";
            SyncState = OperationState<PlayerProfile>.ToError("Account sync cancelled.");
        }
        catch (Exception ex)
        {
            _composition.EventLog.Error("account-sync", ComlinkErrorFormatter.Describe(ex, "Account sync"));
            SyncState = OperationState<PlayerProfile>.ToError(
                $"{ComlinkErrorFormatter.Describe(ex, "Account sync")}. Existing cached data was preserved.");
            SyncProgressText = "Account sync failed; existing cached data was preserved.";
        }
        finally
        {
            _syncCancellation?.Dispose();
            _syncCancellation = null;
        }
    }

    [RelayCommand]
    private async Task RetryStartupAsync()
    {
        try
        {
            _context.InitializeDatabase();
            StartupState = OperationState<bool>.ToSuccess(true);
            await LoadCachedAccountsAsync();
            await LoadFeatureDataAsync();
        }
        catch (Exception ex)
        {
            _composition.EventLog.Error("startup-retry", "Local cache retry failed.");
            StartupState = OperationState<bool>.ToError(
                $"Local cache initialization failed: {ex.Message}");
        }
    }

    private async Task LoadFeatureDataAsync()
    {
        await ModThresholdsViewModel.LoadThresholdsAsync();
        await CharactersViewModel.LoadCharactersAsync();
        await CharacterPrioritiesViewModel.LoadCharactersAsync();
        await ModsViewModel.LoadModsAsync();
        await ModOptimizerViewModel.LoadCharactersAsync();
        await DiagnosticsViewModel.RefreshAsync();
        await RefreshActiveCacheSummaryAsync();
    }

    private async Task RefreshActiveCacheSummaryAsync()
    {
        if (!AllyCodeValidator.TryNormalize(AllyCode, out var activeAllyCode, out _))
        {
            ActiveCharacterCount = 0;
            ActiveModCount = 0;
            ActiveAccountSummaryText = "No active account cache is selected.";
            NextStepText = HasCachedAccounts
                ? "Choose a cached account to work offline, or enter a nine-digit ally code to sync."
                : "Enter a nine-digit ally code, then choose Sync account to create the local cache.";
            return;
        }

        ActiveCharacterCount = await _context.Characters
            .AsNoTracking()
            .CountAsync(character => character.PlayerAllyCode == activeAllyCode)
            .ConfigureAwait(true);
        ActiveModCount = await _context.Mods
            .AsNoTracking()
            .CountAsync(mod => mod.PlayerAllyCode == activeAllyCode)
            .ConfigureAwait(true);

        var account = CachedAccounts.FirstOrDefault(candidate =>
            string.Equals(candidate.AllyCode, activeAllyCode, StringComparison.Ordinal));
        var accountName = string.IsNullOrWhiteSpace(account?.Name) ? "Active account" : account.Name;
        ActiveAccountSummaryText =
            $"{accountName} ({activeAllyCode}) — {ActiveCharacterCount} character(s), {ActiveModCount} mod(s) cached locally.";
        NextStepText = HasActiveCache
            ? "Choose a screen below to inspect the cached account, or sync again to refresh it from Comlink."
            : "This ally code has no cached roster yet. Choose Sync account to fetch it from Comlink.";
    }

    private async Task LoadCachedAccountsAsync()
    {
        var accounts = await _context.Players
            .AsNoTracking()
            .OrderBy(player => player.Name)
            .ThenBy(player => player.AllyCode)
            .ToListAsync()
            .ConfigureAwait(true);

        CachedAccounts.Clear();
        foreach (var account in accounts)
        {
            CachedAccounts.Add(account);
        }

        OnPropertyChanged(nameof(HasCachedAccounts));
        RemoveCachedAccountCommand.NotifyCanExecuteChanged();

        SelectedCachedAccount = CachedAccounts.FirstOrDefault(account =>
            string.Equals(account.AllyCode, AllyCode.Trim(), StringComparison.Ordinal));
    }

    private async Task UseCachedAccountAsync()
    {
        if (SelectedCachedAccount == null)
        {
            return;
        }

        try
        {
            AllyCode = SelectedCachedAccount.AllyCode;
            _lastSyncAllyCode = AllyCode;
            await _settingsService.SaveSettingsAsync(
                _settingsService.CurrentSettings with { DefaultAllyCode = AllyCode });
            SyncState = OperationState<PlayerProfile>.ToEmpty();
            _syncStatusOverride =
                $"Using cached account {SelectedCachedAccount.Name} ({SelectedCachedAccount.AllyCode}).";
            OnPropertyChanged(nameof(SyncStatusText));
            await LoadFeatureDataAsync();
        }
        catch (Exception ex)
        {
            _syncStatusOverride = string.Empty;
            SyncState = OperationState<PlayerProfile>.ToError(
                $"Cached account switch failed: {ex.Message}");
        }
    }

    private bool CanRemoveCachedAccount() =>
        SelectedCachedAccount != null && !IsSyncing && ConfirmAccountRemoval;

    private async Task RemoveCachedAccountAsync()
    {
        if (SelectedCachedAccount == null)
        {
            AccountManagementStatusText = "Select a cached account before removing it.";
            return;
        }

        if (!ConfirmAccountRemoval)
        {
            AccountManagementStatusText =
                "Check the confirmation box before removing the cached account.";
            return;
        }

        var account = SelectedCachedAccount;
        var allyCode = account.AllyCode;
        var wasActiveAccount = string.Equals(
            AllyCode.Trim(),
            allyCode,
            StringComparison.Ordinal);

        try
        {
            var removed = await _composition.PlayerRepository
                .DeletePlayerAsync(allyCode)
                .ConfigureAwait(true);
            if (!removed)
            {
                AccountManagementStatusText =
                    $"Cached account {allyCode} was already removed or could not be found.";
                return;
            }

            ConfirmAccountRemoval = false;
            if (wasActiveAccount)
            {
                _lastSyncAllyCode = string.Empty;
                AllyCode = string.Empty;
                _syncStatusOverride = string.Empty;
                SyncState = OperationState<PlayerProfile>.ToEmpty();
            }

            await LoadCachedAccountsAsync();
            await LoadFeatureDataAsync();
            AccountManagementStatusText = $"Removed cached account {allyCode} and its roster/mod data.";
            _composition.EventLog.Info("account-management", "Cached account and account-owned rows removed.");
            OnPropertyChanged(nameof(SyncStatusText));
        }
        catch (Exception ex)
        {
            _composition.EventLog.Error("account-management", "Cached account removal failed.");
            AccountManagementStatusText = $"Failed to remove cached account: {ex.Message}";
        }
    }

    private async Task RefreshAfterSettingsImportAsync()
    {
        await LoadCachedAccountsAsync();
        await LoadFeatureDataAsync();
    }

    private bool CanSync() =>
        !IsSyncing &&
        AllyCodeValidator.TryNormalize(AllyCode, out _, out _);

    [RelayCommand(CanExecute = nameof(CanRetrySyncCommand))]
    private async Task RetrySyncAsync()
    {
        if (string.IsNullOrWhiteSpace(_lastSyncAllyCode))
        {
            return;
        }

        AllyCode = _lastSyncAllyCode;
        await SyncAsync();
    }

    private bool CanRetrySyncCommand() =>
        !IsSyncing &&
        SyncState.Status == OperationStatus.Error &&
        !string.IsNullOrWhiteSpace(_lastSyncAllyCode);

    [RelayCommand]
    private void CancelSync()
    {
        _syncCancellation?.Cancel();
    }

    public bool CanCancelSync => IsSyncing;

    public bool CanRetrySync => CanRetrySyncCommand();

    private void ApplyComlinkUrl(string url)
    {
        _comlinkClient.BaseAddress = new Uri(url.TrimEnd('/') + "/", UriKind.Absolute);
    }

    private async Task TestComlinkAsync()
    {
        await _composition.ComlinkService.FetchMetaDataRawAsync();
    }

    private async Task ResetCacheAsync()
    {
        if (IsSyncing)
        {
            throw new InvalidOperationException("Cancel the active account sync before resetting the cache.");
        }

        await _context.ResetDatabaseAsync();
        _composition.EventLog.Warning("cache-reset", "Local cache was reset by the user.");
        SyncState = OperationState<PlayerProfile>.ToEmpty();
        await LoadCachedAccountsAsync();
        await LoadFeatureDataAsync();
    }

    private Task<string> BackupCacheAsync() => _context.BackupDatabaseAsync();

    private async Task RestoreCacheAsync(string backupPath)
    {
        if (IsSyncing)
        {
            throw new InvalidOperationException("Cancel the active account sync before restoring the cache.");
        }

        await _context.RestoreDatabaseAsync(backupPath);
        _composition.EventLog.Info("cache-restore", "Local cache was restored from a verified backup.");
        SyncState = OperationState<PlayerProfile>.ToEmpty();
        await LoadCachedAccountsAsync();
        await LoadFeatureDataAsync();
    }

    [RelayCommand]
    private void GoToHome()
    {
        CurrentView = this;
    }

    [RelayCommand]
    private void GoToCharacters()
    {
        CurrentView = CharactersViewModel;
    }

    [RelayCommand]
    private void GoToPriorities()
    {
        CurrentView = CharacterPrioritiesViewModel;
    }

    [RelayCommand]
    private void GoToMods()
    {
        CurrentView = ModsViewModel;
    }

    [RelayCommand]
    private void GoToOptimizer()
    {
        CurrentView = ModOptimizerViewModel;
    }

    [RelayCommand]
    private void GoToThresholds()
    {
        CurrentView = ModThresholdsViewModel;
    }

    [RelayCommand]
    private void GoToSettings()
    {
        CurrentView = SettingsViewModel;
    }

    [RelayCommand]
    private async Task GoToDiagnostics()
    {
        CurrentView = DiagnosticsViewModel;
        await DiagnosticsViewModel.RefreshAsync();
    }
}
