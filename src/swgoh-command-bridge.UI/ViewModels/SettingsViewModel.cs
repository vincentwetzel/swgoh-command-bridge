#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using swgoh_command_bridge.UI;

namespace swgoh_command_bridge.UI.ViewModels;

public class SettingsViewModel : StateViewModelBase<bool>
{
    private readonly ISettingsService _settingsService;
    private readonly Action<string> _applyComlinkUrl;
    private readonly Action<string> _applyAllyCode;
    private readonly Func<Task> _testComlink;
    private readonly Func<Task>? _resetCache;
    private readonly Func<Task<string>>? _backupCache;
    private readonly Func<string, Task>? _restoreCache;
    private readonly Func<Task>? _refreshAfterImport;
    private readonly ICharacterCatalogSnapshotService? _characterCatalogService;
    private readonly Func<Task>? _refreshAfterCatalogImport;
    private readonly DiagnosticEventLog _eventLog;
    private readonly Action<string> _applyTheme;
    private readonly SettingsTransferService _transferService = new();
    private string _comlinkBaseUrl;
    private string _defaultAllyCode;
    private string _theme;
    private bool _enableLocalRecommendationScraping;
    private string _recommendationContactEmail;
    private string _backupStatusText = "No cache backup created in this session.";
    private string _restoreBackupPath = string.Empty;
    private string _restoreStatusText = "No cache restore performed in this session.";
    private string _settingsTransferPath = string.Empty;
    private string _settingsTransferStatusText = "No settings transfer performed in this session.";
    private string _characterCatalogPath = string.Empty;
    private string _shipCatalogPath = string.Empty;
    private string _catalogUpdateStatusText = "Catalog updates are unavailable in this application composition.";
    private bool _confirmCacheReset;
    private bool _confirmCacheRestore;

    public IReadOnlyList<string> ThemeOptions { get; } =
        new[] { ThemePreference.Dark, ThemePreference.Light, ThemePreference.System };

    public SettingsViewModel(
        ISettingsService settingsService,
        Action<string> applyComlinkUrl,
        Action<string> applyAllyCode,
        Func<Task> testComlink,
        Func<Task>? resetCache = null,
        Func<Task<string>>? backupCache = null,
        Func<string, Task>? restoreCache = null,
        Func<Task>? refreshAfterImport = null,
        DiagnosticEventLog? eventLog = null,
        Action<string>? applyTheme = null,
        ICharacterCatalogSnapshotService? characterCatalogService = null,
        Func<Task>? refreshAfterCatalogImport = null)
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
        _refreshAfterImport = refreshAfterImport;
        _eventLog = eventLog ?? new DiagnosticEventLog();
        _applyTheme = applyTheme ?? ThemeManager.Apply;
        _characterCatalogService = characterCatalogService;
        _refreshAfterCatalogImport = refreshAfterCatalogImport;
        _comlinkBaseUrl = settingsService.CurrentSettings.ComlinkBaseUrl;
        _defaultAllyCode = settingsService.CurrentSettings.DefaultAllyCode ?? string.Empty;
        _theme = ThemePreference.Normalize(settingsService.CurrentSettings.Theme);
        _enableLocalRecommendationScraping = settingsService.CurrentSettings.EnableLocalRecommendationScraping;
        _recommendationContactEmail = settingsService.CurrentSettings.RecommendationContactEmail ?? string.Empty;
        if (_characterCatalogService != null)
        {
            _catalogUpdateStatusText = _characterCatalogService.GetSnapshotInfo().Summary;
        }
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
        BackupCacheCommand = new AsyncRelayCommand(BackupCacheAsync);
        ResetCacheCommand = new AsyncRelayCommand(ResetCacheAsync);
        RestoreCacheCommand = new AsyncRelayCommand(RestoreCacheAsync);
        ExportSettingsCommand = new AsyncRelayCommand(ExportSettingsAsync);
        ImportSettingsCommand = new AsyncRelayCommand(ImportSettingsAsync);
        ImportCatalogCommand = new AsyncRelayCommand(ImportCatalogAsync);
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

    public bool EnableLocalRecommendationScraping
    {
        get => _enableLocalRecommendationScraping;
        set => SetField(ref _enableLocalRecommendationScraping, value);
    }

    public string RecommendationContactEmail
    {
        get => _recommendationContactEmail;
        set => SetField(ref _recommendationContactEmail, value);
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

    public string SettingsTransferPath
    {
        get => _settingsTransferPath;
        set => SetField(ref _settingsTransferPath, value);
    }

    public string SettingsTransferStatusText
    {
        get => _settingsTransferStatusText;
        private set => SetField(ref _settingsTransferStatusText, value);
    }

    public string CharacterCatalogPath
    {
        get => _characterCatalogPath;
        set => SetField(ref _characterCatalogPath, value);
    }

    public string ShipCatalogPath
    {
        get => _shipCatalogPath;
        set => SetField(ref _shipCatalogPath, value);
    }

    public string CatalogUpdateStatusText
    {
        get => _catalogUpdateStatusText;
        private set => SetField(ref _catalogUpdateStatusText, value);
    }

    protected override void OnStateChanged() =>
        OnPropertyChanged(nameof(StatusText));

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

    public IAsyncRelayCommand ExportSettingsCommand { get; }

    public IAsyncRelayCommand ImportSettingsCommand { get; }

    public IAsyncRelayCommand ImportCatalogCommand { get; }

    private async Task SaveAsync()
    {
        if (!TryGetValidUrl(out var validatedUrl))
        {
            State = OperationState<bool>.ToError("Comlink URL must be an absolute HTTP or HTTPS URL.");
            return;
        }

        if (!TryGetValidContactEmail(out var validatedContactEmail))
        {
            State = OperationState<bool>.ToError(
                "Recommendation contact must be a single valid email address.");
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
                Theme = ThemePreference.Normalize(Theme),
                EnableLocalRecommendationScraping = EnableLocalRecommendationScraping,
                RecommendationContactEmail = validatedContactEmail
            });
            Theme = ThemePreference.Normalize(Theme);
            _applyTheme(Theme);
            State = OperationState<bool>.ToSuccess(true);
            _eventLog.Info("settings", "Application settings saved.");
        }
        catch (Exception ex)
        {
            _eventLog.Error("settings", "Application settings save failed.");
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
            _eventLog.Info("comlink-test", "Comlink connection test succeeded.");
        }
        catch (Exception ex)
        {
            _eventLog.Error("comlink-test", ComlinkErrorFormatter.Describe(ex, "Comlink connection test"));
            State = OperationState<bool>.ToError(
                ComlinkErrorFormatter.Describe(ex, "Comlink connection test"));
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
            _eventLog.Info("cache-backup", "A local cache backup was created.");
        }
        catch (Exception ex)
        {
            _eventLog.Error("cache-backup", "Local cache backup failed.");
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
            _eventLog.Info("cache-restore", "A local cache backup was restored.");
        }
        catch (Exception ex)
        {
            _eventLog.Error("cache-restore", "Local cache restore failed.");
            RestoreStatusText = $"Cache restore failed: {ex.Message}";
            State = OperationState<bool>.ToError($"Failed to restore local cache: {ex.Message}");
        }
    }

    private async Task ExportSettingsAsync()
    {
        SettingsTransferStatusText = string.Empty;
        if (string.IsNullOrWhiteSpace(SettingsTransferPath))
        {
            SettingsTransferStatusText = "Choose a JSON file path before exporting settings.";
            return;
        }

        try
        {
            var path = GetSettingsTransferPath();
            var json = _transferService.Serialize(_settingsService.CurrentSettings);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await WriteTransferFileAsync(path, json);
            SettingsTransferStatusText = $"Exported settings to {path}. Embedded URL credentials are never included.";
            _eventLog.Info("settings-transfer", "Application settings were exported without embedded credentials.");
        }
        catch (Exception ex)
        {
            _eventLog.Error("settings-transfer", "Application settings export failed.");
            SettingsTransferStatusText = $"Failed to export settings: {ex.Message}";
        }
    }

    private async Task ImportSettingsAsync()
    {
        SettingsTransferStatusText = string.Empty;
        if (string.IsNullOrWhiteSpace(SettingsTransferPath))
        {
            SettingsTransferStatusText = "Choose a JSON file path before importing settings.";
            return;
        }

        try
        {
            var path = GetSettingsTransferPath();
            var json = await File.ReadAllTextAsync(path);
            var imported = _transferService.DeserializeAndValidate(json);
            await _settingsService.SaveSettingsAsync(imported);

            ComlinkBaseUrl = imported.ComlinkBaseUrl;
            DefaultAllyCode = imported.DefaultAllyCode ?? string.Empty;
            Theme = ThemePreference.Normalize(imported.Theme);
            EnableLocalRecommendationScraping = imported.EnableLocalRecommendationScraping;
            RecommendationContactEmail = imported.RecommendationContactEmail ?? string.Empty;
            _applyComlinkUrl(imported.ComlinkBaseUrl);
            _applyAllyCode(DefaultAllyCode);
            _applyTheme(Theme);
            if (_refreshAfterImport != null)
            {
                await _refreshAfterImport();
            }
            State = OperationState<bool>.ToSuccess(true);
            SettingsTransferStatusText = $"Imported settings from {path} and refreshed the active account scope.";
            _eventLog.Info("settings-transfer", "Application settings were imported and the active scope refreshed.");
        }
        catch (Exception ex)
        {
            _eventLog.Error("settings-transfer", "Application settings import failed.");
            SettingsTransferStatusText = $"Failed to import settings: {ex.Message}";
        }
    }

    private async Task ImportCatalogAsync()
    {
        if (_characterCatalogService == null)
        {
            CatalogUpdateStatusText = "Catalog updates are unavailable in this application composition.";
            return;
        }

        try
        {
            CatalogUpdateStatusText = "Validating and importing catalog snapshots...";
            var imported = await _characterCatalogService
                .ImportAsync(CharacterCatalogPath, ShipCatalogPath)
                .ConfigureAwait(true);
            if (_refreshAfterCatalogImport != null)
            {
                await _refreshAfterCatalogImport().ConfigureAwait(true);
            }

            CatalogUpdateStatusText = $"Catalog imported and applied. {imported.Summary}";
            _eventLog.Info("character-catalog-import", imported.Summary);
        }
        catch (Exception ex)
        {
            CatalogUpdateStatusText = $"Catalog import failed: {ex.Message}";
            _eventLog.Error("character-catalog-import", "Catalog import failed.");
        }
    }

    private string GetSettingsTransferPath()
    {
        var path = Path.GetFullPath(SettingsTransferPath.Trim());
        if (string.IsNullOrWhiteSpace(Path.GetFileName(path)))
        {
            throw new InvalidDataException("The settings transfer path must include a file name.");
        }

        return path;
    }

    private static async Task WriteTransferFileAsync(string path, string contents)
    {
        var temporaryPath = path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, contents);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private bool TryGetValidUrl(out string url)
    {
        url = ComlinkBaseUrl.Trim();
        return Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrWhiteSpace(parsed.UserInfo);
    }

    private bool TryGetValidContactEmail(out string? email)
    {
        email = string.IsNullOrWhiteSpace(RecommendationContactEmail)
            ? null
            : RecommendationContactEmail.Trim();
        if (email == null)
        {
            return true;
        }

        try
        {
            var parsed = new MailAddress(email);
            return email.Length <= 254 &&
                string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
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
