using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
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
        }
        catch (Exception ex)
        {
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
            () => AllyCode);
        ModThresholdsViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ModThresholdsViewModel.SelectedThreshold))
            {
                ModsViewModel.RefreshThresholdContext();
            }
        };
        SettingsViewModel = new SettingsViewModel(
            _settingsService,
            ApplyComlinkUrl,
            allyCode => AllyCode = allyCode,
            TestComlinkAsync,
            ResetCacheAsync,
            BackupCacheAsync,
            RestoreCacheAsync);
        _currentView = this;
    }

    public async Task InitializeAsync()
    {
        if (HasStartupError)
        {
            return;
        }

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
            OnPropertyChanged(nameof(AllyCode));
            SyncCommand.NotifyCanExecuteChanged();
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
            SyncCommand.NotifyCanExecuteChanged();
            RetrySyncCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsSyncing => SyncState.Status == OperationStatus.Loading;

    public string SyncStatusText => SyncState.Status switch
    {
        OperationStatus.Loading => "Syncing account data...",
        OperationStatus.Success when SyncState.Data != null =>
            $"Synced {SyncState.Data.Characters.Count} characters and {SyncState.Data.Mods.Count} mods.",
        OperationStatus.Error => SyncState.ErrorMessage ?? "Sync failed.",
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
        var allyCode = AllyCode.Trim();
        _lastSyncAllyCode = allyCode;
        _syncCancellation?.Dispose();
        _syncCancellation = new CancellationTokenSource();
        SyncState = OperationState<PlayerProfile>.ToLoading();

        try
        {
            var profile = await _playerService.SyncPlayerProfileAsync(
                allyCode,
                _syncCancellation.Token);
            await _settingsService.SaveSettingsAsync(
                _settingsService.CurrentSettings with { DefaultAllyCode = allyCode });

            await CharactersViewModel.LoadCharactersAsync();
            await CharacterPrioritiesViewModel.LoadCharactersAsync();
            await ModsViewModel.LoadModsAsync();
            await ModOptimizerViewModel.LoadCharactersAsync();
            await DiagnosticsViewModel.RefreshAsync();
            SyncState = OperationState<PlayerProfile>.ToSuccess(profile);
        }
        catch (OperationCanceledException)
        {
            SyncState = OperationState<PlayerProfile>.ToError("Account sync cancelled.");
        }
        catch (Exception ex)
        {
            SyncState = OperationState<PlayerProfile>.ToError(
                $"Account sync failed: {ex.Message}");
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
            await LoadFeatureDataAsync();
        }
        catch (Exception ex)
        {
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
    }

    private bool CanSync() => !IsSyncing && !string.IsNullOrWhiteSpace(AllyCode);

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
        SyncState = OperationState<PlayerProfile>.ToEmpty();
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
        SyncState = OperationState<PlayerProfile>.ToEmpty();
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
    private void GoToDiagnostics()
    {
        CurrentView = DiagnosticsViewModel;
        _ = DiagnosticsViewModel.RefreshAsync();
    }
}
