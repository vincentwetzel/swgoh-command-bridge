#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;

namespace swgoh_command_bridge.UI.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly Action<string> _applyComlinkUrl;
    private readonly Action<string> _applyAllyCode;
    private readonly Func<Task> _testComlink;
    private readonly Func<Task>? _resetCache;
    private readonly Func<Task<string>>? _backupCache;
    private readonly Func<string, Task>? _restoreCache;
    private string _comlinkBaseUrl;
    private string _defaultAllyCode;
    private string _theme;
    private string _backupStatusText = "No cache backup created in this session.";
    private string _restoreBackupPath = string.Empty;
    private string _restoreStatusText = "No cache restore performed in this session.";
    private bool _confirmCacheReset;
    private bool _confirmCacheRestore;
    private OperationState<bool> _state = OperationState<bool>.ToEmpty();

    public SettingsViewModel(
        ISettingsService settingsService,
        Action<string> applyComlinkUrl,
        Action<string> applyAllyCode,
        Func<Task> testComlink,
        Func<Task>? resetCache = null,
        Func<Task<string>>? backupCache = null,
        Func<string, Task>? restoreCache = null)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(applyComlinkUrl);
        ArgumentNullException.ThrowIfNull(applyAllyCode);
        ArgumentNullException.ThrowIfNull(testComlink);

        _settingsService = settingsService;
        _applyComlinkUrl = applyComlinkUrl;
        _applyAllyCode = applyAllyCode;
        _testComlink = testComlink;
        _resetCache = resetCache;
        _backupCache = backupCache;
        _restoreCache = restoreCache;
        _comlinkBaseUrl = settingsService.CurrentSettings.ComlinkBaseUrl;
        _defaultAllyCode = settingsService.CurrentSettings.DefaultAllyCode ?? string.Empty;
        _theme = settingsService.CurrentSettings.Theme;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
        BackupCacheCommand = new AsyncRelayCommand(BackupCacheAsync);
        ResetCacheCommand = new AsyncRelayCommand(ResetCacheAsync);
        RestoreCacheCommand = new AsyncRelayCommand(RestoreCacheAsync);
    }

    public string HeaderText => "Application Settings";

    public string ComlinkBaseUrl
    {
        get => _comlinkBaseUrl;
        set => SetField(ref _comlinkBaseUrl, value);
    }

    public string DefaultAllyCode
    {
        get => _defaultAllyCode;
        set => SetField(ref _defaultAllyCode, value);
    }

    public string Theme
    {
        get => _theme;
        set => SetField(ref _theme, value);
    }

    public bool ConfirmCacheReset
    {
        get => _confirmCacheReset;
        set => SetField(ref _confirmCacheReset, value);
    }

    public string BackupStatusText
    {
        get => _backupStatusText;
        private set => SetField(ref _backupStatusText, value);
    }

    public string RestoreBackupPath
    {
        get => _restoreBackupPath;
        set => SetField(ref _restoreBackupPath, value);
    }

    public string RestoreStatusText
    {
        get => _restoreStatusText;
        private set => SetField(ref _restoreStatusText, value);
    }

    public bool ConfirmCacheRestore
    {
        get => _confirmCacheRestore;
        set => SetField(ref _confirmCacheRestore, value);
    }

    public OperationState<bool> State
    {
        get => _state;
        private set
        {
            _state = value;
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public bool IsBusy => State.Status == OperationStatus.Loading;

    public bool HasError => State.Status == OperationStatus.Error;

    public string StatusText => State.Status switch
    {
        OperationStatus.Loading => "Working...",
        OperationStatus.Success => "Settings saved or connection succeeded.",
        OperationStatus.Error => State.ErrorMessage ?? "Operation failed.",
        _ => "Changes are applied when saved."
    };

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand TestConnectionCommand { get; }

    public IAsyncRelayCommand BackupCacheCommand { get; }

    public IAsyncRelayCommand ResetCacheCommand { get; }

    public IAsyncRelayCommand RestoreCacheCommand { get; }

    private async Task SaveAsync()
    {
        if (!TryGetValidUrl(out var validatedUrl))
        {
            State = OperationState<bool>.ToError("Comlink URL must be an absolute HTTP or HTTPS URL.");
            return;
        }

        State = OperationState<bool>.ToLoading();
        try
        {
            _applyComlinkUrl(validatedUrl);
            _applyAllyCode(DefaultAllyCode.Trim());
            await _settingsService.SaveSettingsAsync(_settingsService.CurrentSettings with
            {
                ComlinkBaseUrl = validatedUrl,
                DefaultAllyCode = string.IsNullOrWhiteSpace(DefaultAllyCode) ? null : DefaultAllyCode.Trim(),
                Theme = Theme.Trim()
            });
            State = OperationState<bool>.ToSuccess(true);
        }
        catch (Exception ex)
        {
            State = OperationState<bool>.ToError($"Failed to save settings: {ex.Message}");
        }
    }

    private async Task TestConnectionAsync()
    {
        if (!TryGetValidUrl(out var validatedUrl))
        {
            State = OperationState<bool>.ToError("Comlink URL must be an absolute HTTP or HTTPS URL.");
            return;
        }

        State = OperationState<bool>.ToLoading();
        try
        {
            _applyComlinkUrl(validatedUrl);
            await _testComlink();
            State = OperationState<bool>.ToSuccess(true);
        }
        catch (Exception ex)
        {
            State = OperationState<bool>.ToError($"Comlink connection failed: {ex.Message}");
        }
    }

    private async Task ResetCacheAsync()
    {
        if (!ConfirmCacheReset)
        {
            State = OperationState<bool>.ToError(
                "Check the confirmation box before resetting the local cache.");
            return;
        }

        if (_resetCache == null)
        {
            State = OperationState<bool>.ToError(
                "Cache reset is unavailable in this application composition.");
            return;
        }

        State = OperationState<bool>.ToLoading();
        try
        {
            await _resetCache();
            ConfirmCacheReset = false;
            State = OperationState<bool>.ToSuccess(true);
        }
        catch (Exception ex)
        {
            State = OperationState<bool>.ToError($"Failed to reset local cache: {ex.Message}");
        }
    }

    private async Task BackupCacheAsync()
    {
        if (_backupCache == null)
        {
            State = OperationState<bool>.ToError(
                "Cache backup is unavailable in this application composition.");
            return;
        }

        State = OperationState<bool>.ToLoading();
        try
        {
            var backupPath = await _backupCache();
            BackupStatusText = $"Backup created: {backupPath}";
            State = OperationState<bool>.ToSuccess(true);
        }
        catch (Exception ex)
        {
            State = OperationState<bool>.ToError($"Failed to back up local cache: {ex.Message}");
        }
    }

    private async Task RestoreCacheAsync()
    {
        if (!ConfirmCacheRestore)
        {
            State = OperationState<bool>.ToError(
                "Check the confirmation box before restoring the local cache.");
            return;
        }

        if (_restoreCache == null)
        {
            State = OperationState<bool>.ToError(
                "Cache restore is unavailable in this application composition.");
            return;
        }

        if (string.IsNullOrWhiteSpace(RestoreBackupPath))
        {
            State = OperationState<bool>.ToError("Enter the full path to a cache backup file.");
            return;
        }

        State = OperationState<bool>.ToLoading();
        try
        {
            await _restoreCache(RestoreBackupPath.Trim());
            ConfirmCacheRestore = false;
            RestoreStatusText = "Cache restored and feature data reloaded.";
            State = OperationState<bool>.ToSuccess(true);
        }
        catch (Exception ex)
        {
            RestoreStatusText = $"Cache restore failed: {ex.Message}";
            State = OperationState<bool>.ToError($"Failed to restore local cache: {ex.Message}");
        }
    }

    private bool TryGetValidUrl(out string url)
    {
        url = ComlinkBaseUrl.Trim();
        return Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);
    }

    private void SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(name);
    }
}
