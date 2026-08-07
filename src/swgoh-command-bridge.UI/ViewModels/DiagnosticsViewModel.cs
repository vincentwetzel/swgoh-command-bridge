#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;

namespace swgoh_command_bridge.UI.ViewModels;

/// <summary>
/// Provides read-only cache and configuration diagnostics for troubleshooting.
/// </summary>
public sealed class DiagnosticsViewModel : ViewModelBase
{
    private readonly AppDbContext _context;
    private readonly ISettingsService _settingsService;
    private readonly Func<string?> _activeAllyCodeProvider;
    private OperationState<bool> _state = OperationState<bool>.ToEmpty();
    private string _statusText = "Refresh diagnostics to inspect the local cache.";
    private string _exportStatusText = string.Empty;
    private string _comlinkEndpoint = "Not loaded";
    private string _redactedAllyCode = "Not configured";
    private string _settingsPath = "Unavailable";
    private string _cachePath = "Unavailable";
    private string _backupDirectory = "Unavailable";
    private string _lastScrapeSummary = "No completed recommendation refresh has been recorded.";
    private int _playerCount;
    private int _characterCount;
    private int _modCount;
    private int _recommendationCount;

    public DiagnosticsViewModel(
        AppDbContext context,
        ISettingsService settingsService,
        Func<string?> activeAllyCodeProvider)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(activeAllyCodeProvider);

        _context = context;
        _settingsService = settingsService;
        _activeAllyCodeProvider = activeAllyCodeProvider;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ExportCommand = new AsyncRelayCommand(ExportAsync);
    }

    public string HeaderText => "Diagnostics";

    public OperationState<bool> State
    {
        get => _state;
        private set
        {
            _state = value;
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public bool IsLoading => State.Status == OperationStatus.Loading;

    public bool HasError => State.Status == OperationStatus.Error;

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public string ExportStatusText
    {
        get => _exportStatusText;
        private set => SetField(ref _exportStatusText, value);
    }

    public string ComlinkEndpoint
    {
        get => _comlinkEndpoint;
        private set => SetField(ref _comlinkEndpoint, value);
    }

    public string RedactedAllyCode
    {
        get => _redactedAllyCode;
        private set => SetField(ref _redactedAllyCode, value);
    }

    public string SettingsPath
    {
        get => _settingsPath;
        private set => SetField(ref _settingsPath, value);
    }

    public string CachePath
    {
        get => _cachePath;
        private set => SetField(ref _cachePath, value);
    }

    public string BackupDirectory
    {
        get => _backupDirectory;
        private set => SetField(ref _backupDirectory, value);
    }

    public string LastScrapeSummary
    {
        get => _lastScrapeSummary;
        private set => SetField(ref _lastScrapeSummary, value);
    }

    public int PlayerCount
    {
        get => _playerCount;
        private set => SetField(ref _playerCount, value);
    }

    public int CharacterCount
    {
        get => _characterCount;
        private set => SetField(ref _characterCount, value);
    }

    public int ModCount
    {
        get => _modCount;
        private set => SetField(ref _modCount, value);
    }

    public int RecommendationCount
    {
        get => _recommendationCount;
        private set => SetField(ref _recommendationCount, value);
    }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand ExportCommand { get; }

    public async Task RefreshAsync()
    {
        State = OperationState<bool>.ToLoading();
        var settings = _settingsService.CurrentSettings;
        ComlinkEndpoint = FormatComlinkEndpoint(settings.ComlinkBaseUrl);
        RedactedAllyCode = RedactAllyCode(_activeAllyCodeProvider());
        SettingsPath = _settingsService.SettingsPath;
        CachePath = _context.CachePath ?? "Unavailable or in-memory database";
        BackupDirectory = _context.CacheBackupDirectory ?? "Unavailable";
        LastScrapeSummary = FormatScrapeSummary(settings.LastRecommendationScrape);

        try
        {
            if (!await _context.Database.CanConnectAsync().ConfigureAwait(true))
            {
                ClearCounts();
                State = OperationState<bool>.ToError("The local cache could not be reached.");
                return;
            }

            PlayerCount = await _context.Players.AsNoTracking().CountAsync().ConfigureAwait(true);
            CharacterCount = await _context.Characters.AsNoTracking().CountAsync().ConfigureAwait(true);
            ModCount = await _context.Mods.AsNoTracking().CountAsync().ConfigureAwait(true);
            RecommendationCount = await _context.SwgohGgRecommendations
                .AsNoTracking()
                .CountAsync()
                .ConfigureAwait(true);
            State = OperationState<bool>.ToSuccess(true);
        }
        catch (Exception ex)
        {
            ClearCounts();
            State = OperationState<bool>.ToError($"Diagnostics failed: {ex.Message}");
        }
    }

    private async Task ExportAsync()
    {
        ExportStatusText = "Preparing diagnostics report...";
        await RefreshAsync();

        try
        {
            Directory.CreateDirectory(_settingsService.DiagnosticsDirectory);
            var reportPath = Path.Combine(
                _settingsService.DiagnosticsDirectory,
                $"diagnostics-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.txt");
            var report = new StringBuilder()
                .AppendLine("SWGOH Command Bridge diagnostics")
                .AppendLine($"Generated UTC: {DateTime.UtcNow:O}")
                .AppendLine($"Comlink endpoint: {ComlinkEndpoint}")
                .AppendLine($"Ally code: {RedactedAllyCode}")
                .AppendLine($"Settings path: {SettingsPath}")
                .AppendLine($"Cache path: {CachePath}")
                .AppendLine($"Backup directory: {BackupDirectory}")
                .AppendLine($"Players: {PlayerCount}")
                .AppendLine($"Characters: {CharacterCount}")
                .AppendLine($"Mods: {ModCount}")
                .AppendLine($"Recommendations: {RecommendationCount}")
                .AppendLine($"Last recommendation refresh: {LastScrapeSummary}")
                .ToString();

            await File.WriteAllTextAsync(reportPath, report).ConfigureAwait(true);
            ExportStatusText = $"Diagnostics exported: {reportPath}";
        }
        catch (Exception ex)
        {
            ExportStatusText = $"Diagnostics export failed: {ex.Message}";
        }
    }

    private void ClearCounts()
    {
        PlayerCount = 0;
        CharacterCount = 0;
        ModCount = 0;
        RecommendationCount = 0;
    }

    private static string FormatComlinkEndpoint(string configuredUrl)
    {
        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var parsed))
        {
            return "Invalid configured URL";
        }

        return parsed.GetLeftPart(UriPartial.Authority);
    }

    private static string RedactAllyCode(string? allyCode)
    {
        var normalized = allyCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Not configured";
        }

        if (normalized.Length <= 4)
        {
            return "****";
        }

        return new string('*', normalized.Length - 4) + normalized[^4..];
    }

    private static string FormatScrapeSummary(RecommendationScrapeSummary? summary)
    {
        if (summary == null)
        {
            return "No completed recommendation refresh has been recorded.";
        }

        var state = summary.Cancelled ? "cancelled" : "completed";
        return $"{state} {summary.CompletedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}: " +
            $"{summary.Processed} processed, {summary.Succeeded} succeeded, {summary.Failed} failed.";
    }

    private void SetField<T>(
        ref T field,
        T value,
        [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(name);
    }
}
