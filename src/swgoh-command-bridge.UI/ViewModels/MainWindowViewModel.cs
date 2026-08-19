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
    private string _syncSummaryText = string.Empty;
    private string _accountManagementStatusText = string.Empty;
    private string _accountSearchText = string.Empty;
    private string _activeAccountSummaryText = "No active account cache is selected.";
    private string _activeCacheFreshnessText = "Sync freshness is unavailable for the active cache.";
    private string _activeSyncOutcomeText = "No sync attempt is recorded for the active account.";
    private string _nextStepText = "Open the account switcher above to add or choose an account.";
    private string _startupProgressText = string.Empty;
    private double _startupProgressPercent;
    private bool _startupProgressIndeterminate;
    private int _activeCharacterCount;
    private int _activeModCount;
    private bool _isActiveCacheStale;
    private bool _confirmAccountRemoval;
    private bool _isAddingAccount;
    private Task? _activeAccountRefreshTask;
    private Task? _catalogRefreshTask;
    private Task? _preferredModsRefreshTask;
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

        CharactersViewModel = new CharactersViewModel(
            _context,
            () => AllyCode,
            composition.CharacterCatalogService,
            composition.EventLog,
            composition.PreferredModsService);
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
            composition.EventLog,
            ThemeManager.Apply);
        _currentView = this;
    }

    public ObservableCollection<PlayerEntity> CachedAccounts { get; } = new();

    public ObservableCollection<PlayerEntity> VisibleCachedAccounts { get; } = new();

    public bool HasCachedAccounts => CachedAccounts.Count > 0;

    public bool HasNoCachedAccounts => !HasCachedAccounts;

    public bool HasVisibleCachedAccounts => VisibleCachedAccounts.Count > 0;

    public bool HasNoVisibleCachedAccounts => HasCachedAccounts && !HasVisibleCachedAccounts;

    public bool HasActiveAccount => FindActiveCachedAccount() != null ||
        AllyCodeValidator.TryNormalize(AllyCode, out _, out _);

    public bool IsAddingAccount
    {
        get => _isAddingAccount;
        private set
        {
            if (_isAddingAccount == value)
            {
                return;
            }

            _isAddingAccount = value;
            OnPropertyChanged(nameof(IsAddingAccount));
            OnPropertyChanged(nameof(IsNotAddingAccount));
            OnPropertyChanged(nameof(ShowActiveAccountSummary));
        }
    }

    public bool IsNotAddingAccount => !IsAddingAccount;

    public bool ShowActiveAccountSummary => HasActiveAccount && IsNotAddingAccount;

    public string ActiveAccountDisplayName
    {
        get
        {
            var account = FindActiveCachedAccount();
            if (!string.IsNullOrWhiteSpace(account?.Name))
            {
                return account.Name;
            }

            return AllyCodeValidator.TryNormalize(AllyCode, out var allyCode, out _)
                ? $"Account {allyCode}"
                : "Select account";
        }
    }

    public string ActiveAccountDisplayCode => IsSyncing
        ? "Refreshing..."
        : FindActiveCachedAccount()?.AllyCode ??
          (AllyCodeValidator.TryNormalize(AllyCode, out var allyCode, out _) ? allyCode : string.Empty);

    public string AccountSearchText
    {
        get => _accountSearchText;
        set
        {
            if (_accountSearchText == value)
            {
                return;
            }

            _accountSearchText = value;
            OnPropertyChanged(nameof(AccountSearchText));
            ApplyCachedAccountFilter();
        }
    }

    public string CachedAccountFilterStatusText => !HasCachedAccounts
        ? string.Empty
        : string.IsNullOrWhiteSpace(AccountSearchText)
            ? $"{CachedAccounts.Count} cached account(s)."
            : $"Showing {VisibleCachedAccounts.Count} of {CachedAccounts.Count} cached account(s).";

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

    public string ActiveCacheFreshnessText
    {
        get => _activeCacheFreshnessText;
        private set
        {
            if (_activeCacheFreshnessText == value)
            {
                return;
            }

            _activeCacheFreshnessText = value;
            OnPropertyChanged(nameof(ActiveCacheFreshnessText));
        }
    }

    public string ActiveSyncOutcomeText
    {
        get => _activeSyncOutcomeText;
        private set
        {
            if (_activeSyncOutcomeText == value)
            {
                return;
            }

            _activeSyncOutcomeText = value;
            OnPropertyChanged(nameof(ActiveSyncOutcomeText));
        }
    }

    public bool IsActiveCacheStale
    {
        get => _isActiveCacheStale;
        private set
        {
            if (_isActiveCacheStale == value)
            {
                return;
            }

            _isActiveCacheStale = value;
            OnPropertyChanged(nameof(IsActiveCacheStale));
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

        await PrepareComlinkAsync();
        await LoadCachedAccountsAsync();
        await LoadFeatureDataAsync();
        StartCatalogRefresh();
        StartPreferredModsRefresh();
        StartStaleActiveAccountRefresh();
    }

    public string StartupProgressText
    {
        get => _startupProgressText;
        private set
        {
            if (_startupProgressText == value)
            {
                return;
            }

            _startupProgressText = value;
            OnPropertyChanged(nameof(StartupProgressText));
            OnPropertyChanged(nameof(HasStartupProgress));
        }
    }

    public double StartupProgressPercent
    {
        get => _startupProgressPercent;
        private set
        {
            if (Math.Abs(_startupProgressPercent - value) < 0.01)
            {
                return;
            }

            _startupProgressPercent = value;
            OnPropertyChanged(nameof(StartupProgressPercent));
        }
    }

    public bool IsStartupProgressIndeterminate
    {
        get => _startupProgressIndeterminate;
        private set
        {
            if (_startupProgressIndeterminate == value)
            {
                return;
            }

            _startupProgressIndeterminate = value;
            OnPropertyChanged(nameof(IsStartupProgressIndeterminate));
        }
    }

    public bool HasStartupProgress => !string.IsNullOrWhiteSpace(StartupProgressText);

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
            OnPropertyChanged(nameof(HasActiveAccount));
            OnPropertyChanged(nameof(ShowActiveAccountSummary));
            OnPropertyChanged(nameof(ActiveAccountDisplayName));
            OnPropertyChanged(nameof(ActiveAccountDisplayCode));
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
            OnPropertyChanged(nameof(HasActiveAccount));
            OnPropertyChanged(nameof(ShowActiveAccountSummary));
            OnPropertyChanged(nameof(ActiveAccountDisplayName));
            OnPropertyChanged(nameof(ActiveAccountDisplayCode));
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
            OnPropertyChanged(nameof(ActiveAccountDisplayCode));
            OnPropertyChanged(nameof(CanCancelSync));
            OnPropertyChanged(nameof(CanRetrySync));
            OnPropertyChanged(nameof(SyncStatusText));
            OnPropertyChanged(nameof(SyncDiagnosticsText));
            OnPropertyChanged(nameof(HasSyncDiagnostics));
            OnPropertyChanged(nameof(HasSyncSummary));
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

    public string SyncSummaryText
    {
        get => _syncSummaryText;
        private set
        {
            if (_syncSummaryText == value)
            {
                return;
            }

            _syncSummaryText = value;
            OnPropertyChanged(nameof(SyncSummaryText));
            OnPropertyChanged(nameof(HasSyncSummary));
        }
    }

    public bool HasSyncSummary => !string.IsNullOrWhiteSpace(SyncSummaryText);

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
        SyncSummaryText = string.Empty;
        SyncProgressText = "Connecting to Comlink...";
        OnPropertyChanged(nameof(SyncStatusText));
        _syncCancellation?.Dispose();
        _syncCancellation = new CancellationTokenSource();
        SyncState = OperationState<PlayerProfile>.ToLoading();

        try
        {
            _composition.EventLog.Info("account-sync", "Ensuring Comlink is ready for account sync.");
            await _composition.EnsureComlinkReadyAsync(
                new Progress<ComlinkRuntimeProgress>(update => SyncProgressText = update.Message),
                _syncCancellation.Token);
            _composition.EventLog.Info("account-sync", "Comlink is ready; requesting the player profile.");
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
            SyncSummaryText =
                $"Completed sync for {allyCode}: {profile.Characters.Count} characters, " +
                $"{profile.Mods.Count} mods, {profile.Diagnostics.Warnings.Count} parser warnings.";
            _composition.EventLog.Info(
                "account-sync",
                $"Account sync completed with {profile.Characters.Count} characters and {profile.Mods.Count} mods.");
        }
        catch (OperationCanceledException)
        {
            _composition.EventLog.Warning("account-sync", "Account sync was cancelled.");
            SyncProgressText = "Account sync cancelled.";
            SyncSummaryText = $"Cancelled sync for {allyCode}; existing cached data was preserved.";
            SyncState = OperationState<PlayerProfile>.ToError("Account sync cancelled.");
        }
        catch (Exception ex)
        {
            var failure = ComlinkErrorFormatter.Describe(ex, "Account sync");
            var detail = ex.GetType().Name + ": " + ex.Message;
            if (ex.InnerException != null)
            {
                detail += " Inner=" + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message;
            }

            _composition.EventLog.Error("account-sync", failure + " [" + detail + "]");
            SyncState = OperationState<PlayerProfile>.ToError(
                $"{failure}. Existing cached data was preserved.");
            SyncProgressText = "Account sync failed; existing cached data was preserved.";
            SyncSummaryText =
                $"Sync failed for {allyCode}; existing cached data was preserved. " +
                failure;
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
            await PrepareComlinkAsync();
            await LoadCachedAccountsAsync();
            await LoadFeatureDataAsync();
            StartStaleActiveAccountRefresh();
        }
        catch (Exception ex)
        {
            _composition.EventLog.Error("startup-retry", "Local cache retry failed.");
            StartupState = OperationState<bool>.ToError(
                $"Local cache initialization failed: {ex.Message}");
        }
    }

    private async Task PrepareComlinkAsync()
    {
        StartupState = OperationState<bool>.ToLoading();
        StartupProgressText = "Preparing the account service...";
        StartupProgressPercent = 0;
        IsStartupProgressIndeterminate = true;

        try
        {
            var progress = new Progress<ComlinkRuntimeProgress>(update =>
            {
                StartupProgressText = update.Message;
                if (update.Percent.HasValue)
                {
                    StartupProgressPercent = update.Percent.Value;
                    IsStartupProgressIndeterminate = false;
                }
                else
                {
                    IsStartupProgressIndeterminate = true;
                }
            });

            var result = await _composition.EnsureComlinkReadyAsync(progress).ConfigureAwait(true);
            _composition.EventLog.Info(
                "startup-comlink",
                $"Account service ready at {result.BaseAddress} (managed locally: {result.ManagedLocally}).");
            StartupState = OperationState<bool>.ToSuccess(true);
        }
        catch (Exception ex)
        {
            _composition.EventLog.Error("startup-comlink", "Account service setup failed.");
            StartupState = OperationState<bool>.ToError(
                $"Account service setup failed: {ex.Message} Cached data remains available offline.");
        }
        finally
        {
            StartupProgressText = string.Empty;
            IsStartupProgressIndeterminate = false;
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

    private void StartStaleActiveAccountRefresh()
    {
        if (HasStartupError || IsSyncing ||
            _activeAccountRefreshTask is { IsCompleted: false })
        {
            return;
        }

        _activeAccountRefreshTask = RefreshStaleActiveAccountAsync();
    }

    private async Task RefreshStaleActiveAccountAsync()
    {
        if (!IsActiveCacheStale ||
            !AllyCodeValidator.TryNormalize(AllyCode, out _, out _) ||
            FindActiveCachedAccount() == null)
        {
            return;
        }

        _composition.EventLog.Info(
            "account-sync",
            "A stale active account cache will be refreshed in the background.");

        try
        {
            await SyncAsync();
        }
        catch (Exception ex)
        {
            _composition.EventLog.Error(
                "account-sync",
                $"Background account refresh failed: {ComlinkErrorFormatter.Describe(ex, "Account refresh")}");
        }
    }

    private async Task RefreshActiveCacheSummaryAsync()
    {
        if (!AllyCodeValidator.TryNormalize(AllyCode, out var activeAllyCode, out _))
        {
            ActiveCharacterCount = 0;
            ActiveModCount = 0;
            ActiveAccountSummaryText = "No active account cache is selected.";
            ActiveCacheFreshnessText = "Sync freshness is unavailable for the active cache.";
            ActiveSyncOutcomeText = "No sync attempt is recorded for the active account.";
            IsActiveCacheStale = false;
            NextStepText = HasCachedAccounts
                ? "Choose a cached account to work offline, or open the account switcher to add one."
                : "Open the account switcher to add an account and sync its local cache.";
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
        var isOlderThan24Hours = account?.LastSyncedUtc is not DateTime lastSynced ||
            lastSynced < DateTime.UtcNow.Subtract(TimeSpan.FromHours(24));
        var hasUnreadableModCache = ActiveModCount > 0 && await _context.Mods
            .AsNoTracking()
            .AnyAsync(mod =>
                mod.PlayerAllyCode == activeAllyCode &&
                (mod.Set <= 0 ||
                 mod.Slot <= 0 ||
                 mod.PrimaryStatType == nameof(StatType.None) ||
                 string.IsNullOrWhiteSpace(mod.SecondaryStatsJson) ||
                 mod.SecondaryStatsJson == "[]"))
            .ConfigureAwait(true);
        IsActiveCacheStale = isOlderThan24Hours || hasUnreadableModCache;
        ActiveCacheFreshnessText = account?.LastSyncedUtc is DateTime synced
            ? $"Last synced {FormatAge(DateTime.UtcNow - synced)} ago ({synced.ToLocalTime():yyyy-MM-dd HH:mm})." +
              (hasUnreadableModCache
                  ? " This cache contains unreadable mod data and will be refreshed."
                  : IsActiveCacheStale ? " This cache is over 24 hours old." : string.Empty)
            : "Last sync time is unavailable for this legacy cache.";
        var latestSync = await _context.SyncHistory
            .AsNoTracking()
            .Where(entry => entry.AllyCode == activeAllyCode)
            .OrderByDescending(entry => entry.StartedUtc)
            .ThenByDescending(entry => entry.Id)
            .FirstOrDefaultAsync()
            .ConfigureAwait(true);
        ActiveSyncOutcomeText = latestSync == null
            ? "No sync attempt is recorded for the active account."
            : FormatSyncOutcome(latestSync);
        NextStepText = HasActiveCache && IsActiveCacheStale
            ? hasUnreadableModCache
                ? "The cached mod data is unreadable. It will be refreshed from Comlink when available."
                : "The cache is over 24 hours old. Sync again when Comlink is available, or continue inspecting the cached data offline."
            : HasActiveCache
                ? "Choose a screen below to inspect the cached account, or sync again to refresh it from Comlink."
            : "This ally code has no cached roster yet. Choose Sync account to fetch it from Comlink.";
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalMinutes < 1)
        {
            return "less than a minute";
        }

        if (age.TotalHours < 1)
        {
            return $"{Math.Max(1, (int)age.TotalMinutes)} minute(s)";
        }

        if (age.TotalDays < 1)
        {
            return $"{Math.Max(1, (int)age.TotalHours)} hour(s)";
        }

        return $"{Math.Max(1, (int)age.TotalDays)} day(s)";
    }

    private static string FormatSyncOutcome(SyncHistoryEntity history)
    {
        var timestamp = (history.CompletedUtc ?? history.StartedUtc).ToLocalTime();
        var state = history.Status switch
        {
            "completed" => $"completed with {history.CharacterCount} character(s), {history.ModCount} mod(s)",
            "cancelled" => "cancelled",
            "failed" => $"failed: {history.ErrorSummary ?? "unknown error"}",
            _ => "still in progress or interrupted"
        };
        var warnings = history.WarningCount > 0
            ? $" {history.WarningCount} parser warning(s) were recorded."
            : string.Empty;
        return $"Last sync {state} at {timestamp:yyyy-MM-dd HH:mm} local.{warnings}";
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
        OnPropertyChanged(nameof(HasNoCachedAccounts));
        OnPropertyChanged(nameof(HasActiveAccount));
        OnPropertyChanged(nameof(ShowActiveAccountSummary));
        OnPropertyChanged(nameof(ActiveAccountDisplayName));
        OnPropertyChanged(nameof(ActiveAccountDisplayCode));
        ApplyCachedAccountFilter();
        RemoveCachedAccountCommand.NotifyCanExecuteChanged();

        SelectedCachedAccount = CachedAccounts.FirstOrDefault(account =>
            string.Equals(account.AllyCode, AllyCode.Trim(), StringComparison.Ordinal));
    }

    private void ApplyCachedAccountFilter()
    {
        var query = AccountSearchText.Trim();
        var filtered = CachedAccounts
            .Where(account => string.IsNullOrWhiteSpace(query) ||
                (account.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                account.AllyCode.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        VisibleCachedAccounts.Clear();
        foreach (var account in filtered)
        {
            VisibleCachedAccounts.Add(account);
        }

        OnPropertyChanged(nameof(HasVisibleCachedAccounts));
        OnPropertyChanged(nameof(HasNoVisibleCachedAccounts));
        OnPropertyChanged(nameof(CachedAccountFilterStatusText));
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
            SyncSummaryText = string.Empty;
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

    [RelayCommand]
    private async Task SwitchAccountAsync(PlayerEntity? account)
    {
        if (account == null || IsSyncing)
        {
            return;
        }

        SelectedCachedAccount = CachedAccounts.FirstOrDefault(candidate =>
            string.Equals(candidate.AllyCode, account.AllyCode, StringComparison.Ordinal)) ?? account;
        await UseCachedAccountAsync();
    }

    [RelayCommand]
    private async Task AddAccountAsync()
    {
        if (IsSyncing)
        {
            return;
        }

        IsAddingAccount = true;
        SelectedCachedAccount = null;
        _lastSyncAllyCode = string.Empty;
        AllyCode = string.Empty;
        _syncStatusOverride = string.Empty;
        SyncSummaryText = string.Empty;
        SyncState = OperationState<PlayerProfile>.ToEmpty();
        AccountManagementStatusText = "Enter the new account's nine-digit ally code, then choose Sync account.";
        OnPropertyChanged(nameof(SyncStatusText));
        await RefreshActiveCacheSummaryAsync();
    }

    [RelayCommand]
    private void CancelAddAccount()
    {
        IsAddingAccount = false;
        AccountManagementStatusText = string.Empty;
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
                SyncSummaryText = string.Empty;
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

    private async Task RefreshAfterCatalogImportAsync()
    {
        await CharactersViewModel.RefreshCatalogAsync();
        await CharacterPrioritiesViewModel.LoadCharactersAsync();
        await ModOptimizerViewModel.LoadCharactersAsync();
    }

    private void StartCatalogRefresh()
    {
        if (_catalogRefreshTask != null)
        {
            return;
        }

        _catalogRefreshTask = RefreshCatalogInBackgroundAsync();
    }

    private async Task RefreshCatalogInBackgroundAsync()
    {
        try
        {
            _composition.EventLog.Info("character-catalog-refresh", "Checking Comlink for a newer character catalog.");
            var snapshot = await _composition.CatalogRefreshService.RefreshAsync().ConfigureAwait(true);
            await RefreshAfterCatalogImportAsync().ConfigureAwait(true);
            _composition.EventLog.Info("character-catalog-refresh", snapshot.Summary);
        }
        catch (Exception ex)
        {
            // Background refresh is best-effort. The last verified local or
            // embedded catalog remains active when Comlink is unavailable.
            _composition.EventLog.Warning(
                "character-catalog-refresh",
                $"Background catalog refresh skipped: {ComlinkErrorFormatter.Describe(ex, "Catalog refresh")}");
        }
    }

    private void StartPreferredModsRefresh()
    {
        if (_preferredModsRefreshTask != null)
        {
            return;
        }

        _preferredModsRefreshTask = RefreshPreferredModsInBackgroundAsync();
    }

    private async Task RefreshPreferredModsInBackgroundAsync()
    {
        try
        {
            var result = await _composition.PreferredModsService
                .RefreshIfDueAsync()
                .ConfigureAwait(true);
            _composition.EventLog.Info("preferred-mods-refresh", result.Message);
        }
        catch (Exception ex)
        {
            // The bundled or last verified dataset remains available. This is
            // intentionally invisible during startup unless Diagnostics is opened.
            _composition.EventLog.Warning(
                "preferred-mods-refresh",
                $"Background preferred-mod refresh skipped: {ex.Message}");
        }
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
        if (_syncCancellation == null)
        {
            return;
        }

        SyncProgressText = "Cancellation requested...";
        _syncCancellation.Cancel();
    }

    public bool CanCancelSync => IsSyncing;

    public bool CanRetrySync => CanRetrySyncCommand();

    private void ApplyComlinkUrl(string url)
    {
        _comlinkClient.BaseAddress = new Uri(url.TrimEnd('/') + "/", UriKind.Absolute);
    }

    private async Task TestComlinkAsync()
    {
        await _composition.EnsureComlinkReadyAsync();
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
        SyncSummaryText = string.Empty;
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
        SyncSummaryText = string.Empty;
        SyncState = OperationState<PlayerProfile>.ToEmpty();
        await LoadCachedAccountsAsync();
        await LoadFeatureDataAsync();
    }

    private PlayerEntity? FindActiveCachedAccount()
    {
        var activeAllyCode = AllyCode.Trim();
        return string.IsNullOrWhiteSpace(activeAllyCode)
            ? null
            : CachedAccounts.FirstOrDefault(account =>
                string.Equals(account.AllyCode, activeAllyCode, StringComparison.Ordinal));
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
