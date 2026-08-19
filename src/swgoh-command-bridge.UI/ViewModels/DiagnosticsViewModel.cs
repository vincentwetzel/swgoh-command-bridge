#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;

namespace swgoh_command_bridge.UI.ViewModels;

/// <summary>
/// Provides read-only cache and configuration diagnostics for troubleshooting.
/// </summary>
public sealed class DiagnosticsViewModel : StateViewModelBase<bool>
{
    private readonly AppDbContext _context;
    private readonly ISettingsService _settingsService;
    private readonly Func<string?> _activeAllyCodeProvider;
    private readonly DiagnosticEventLog _eventLog;
    private string _exportStatusText = string.Empty;
    private string _comlinkEndpoint = "Not loaded";
    private string _redactedAllyCode = "Not configured";
    private string _settingsPath = "Unavailable";
    private string _cachePath = "Unavailable";
    private string _backupDirectory = "Unavailable";
    private string _lastScrapeSummary = "No completed recommendation refresh has been recorded.";
    private string _lastAccountSyncText = "No account sync timestamp is available.";
    private string _lastSyncOutcomeText = "No sync attempt is recorded.";
    private string _recentSyncHistoryText = "No sync attempts are recorded.";
    private string _recentEventsText = "No application events recorded in this session.";
    private string _eventLogPath = "Unavailable";
    private int _playerCount;
    private int _characterCount;
    private int _modCount;
    private int _recommendationCount;

    public DiagnosticsViewModel(
        AppDbContext context,
        ISettingsService settingsService,
        Func<string?> activeAllyCodeProvider,
        DiagnosticEventLog? eventLog = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(activeAllyCodeProvider);

        _context = context;
        _settingsService = settingsService;
        _activeAllyCodeProvider = activeAllyCodeProvider;
        _eventLog = eventLog ?? new DiagnosticEventLog();
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ExportCommand = new AsyncRelayCommand(ExportAsync);
    }

    public string HeaderText => "Diagnostics";

    protected override void OnStateChanged() =>
        OnPropertyChanged(nameof(StatusText));

    public string StatusText => State.Status switch
    {
        OperationStatus.Loading => "Refreshing diagnostics...",
        OperationStatus.Success => "Diagnostics are current.",
        OperationStatus.Error => State.ErrorMessage ?? "Diagnostics refresh failed.",
        _ => "Refresh diagnostics to inspect the local cache."
    };

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

    public string LastAccountSyncText
    {
        get => _lastAccountSyncText;
        private set => SetField(ref _lastAccountSyncText, value);
    }

    public string LastSyncOutcomeText
    {
        get => _lastSyncOutcomeText;
        private set => SetField(ref _lastSyncOutcomeText, value);
    }

    public string RecentSyncHistoryText
    {
        get => _recentSyncHistoryText;
        private set => SetField(ref _recentSyncHistoryText, value);
    }

    public string RecentEventsText
    {
        get => _recentEventsText;
        private set => SetField(ref _recentEventsText, value);
    }

    public string EventLogPath
    {
        get => _eventLogPath;
        private set => SetField(ref _eventLogPath, value);
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
        EventLogPath = _eventLog.PersistentLogPath;
            LastScrapeSummary = FormatScrapeSummary(settings.LastRecommendationScrape);
        RecentEventsText = _eventLog.FormatRecent();

        try
        {
            if (!await _context.Database.CanConnectAsync().ConfigureAwait(true))
            {
                ClearCounts();
                State = OperationState<bool>.ToError("The local cache could not be reached.");
                return;
            }

            PlayerCount = await _context.Players.AsNoTracking().CountAsync().ConfigureAwait(true);
            var latestAccountSync = await _context.Players
                .AsNoTracking()
                .Where(player => player.LastSyncedUtc.HasValue)
                .OrderByDescending(player => player.LastSyncedUtc)
                .Select(player => player.LastSyncedUtc)
                .FirstOrDefaultAsync()
                .ConfigureAwait(true);
            LastAccountSyncText = latestAccountSync is DateTime synced
                ? $"{synced.ToLocalTime():yyyy-MM-dd HH:mm} local"
                : "No account sync timestamp is available (legacy cache or empty cache).";
            var latestSync = await _context.SyncHistory
                .AsNoTracking()
                .OrderByDescending(entry => entry.StartedUtc)
                .ThenByDescending(entry => entry.Id)
                .FirstOrDefaultAsync()
                .ConfigureAwait(true);
            LastSyncOutcomeText = latestSync == null
                ? "No sync attempt is recorded."
                : FormatSyncOutcome(latestSync);
            var recentSyncHistory = await _context.SyncHistory
                .AsNoTracking()
                .OrderByDescending(entry => entry.StartedUtc)
                .ThenByDescending(entry => entry.Id)
                .Take(10)
                .ToListAsync()
                .ConfigureAwait(true);
            RecentSyncHistoryText = FormatRecentSyncHistory(recentSyncHistory);
            CharacterCount = await _context.Characters.AsNoTracking().CountAsync().ConfigureAwait(true);
            ModCount = await _context.Mods.AsNoTracking().CountAsync().ConfigureAwait(true);
            RecommendationCount = await _context.SwgohGgRecommendations
                .AsNoTracking()
                .CountAsync()
                .ConfigureAwait(true);
            State = OperationState<bool>.ToSuccess(true);
            _eventLog.Info("diagnostics", "Local cache diagnostics refreshed.");
        }
        catch (Exception ex)
        {
            ClearCounts();
            _eventLog.Error("diagnostics", "Local cache diagnostics failed.");
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
                .AppendLine($"Application event log: {EventLogPath}")
                .AppendLine($"Players: {PlayerCount}")
                .AppendLine($"Characters: {CharacterCount}")
                .AppendLine($"Mods: {ModCount}")
                .AppendLine($"Recommendations: {RecommendationCount}")
                .AppendLine($"Latest account sync: {LastAccountSyncText}")
                .AppendLine($"Latest sync outcome: {LastSyncOutcomeText}")
                .AppendLine()
                .AppendLine("Recent sync attempts:")
                .AppendLine(RecentSyncHistoryText)
                .AppendLine($"Last recommendation refresh: {LastScrapeSummary}")
                .AppendLine()
                .AppendLine("Recent application events:")
                .AppendLine(_eventLog.FormatRecent())
                .ToString();

            await File.WriteAllTextAsync(reportPath, report).ConfigureAwait(true);
            ExportStatusText = $"Diagnostics exported: {reportPath}";
        }
        catch (Exception ex)
        {
            _eventLog.Error("diagnostics-export", "Diagnostics export failed.");
            ExportStatusText = $"Diagnostics export failed: {ex.Message}";
        }
    }

    private void ClearCounts()
    {
        PlayerCount = 0;
        CharacterCount = 0;
        ModCount = 0;
        RecommendationCount = 0;
        LastAccountSyncText = "Unavailable because the local cache could not be read.";
        LastSyncOutcomeText = "Unavailable because the local cache could not be read.";
        RecentSyncHistoryText = "Unavailable because the local cache could not be read.";
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

    private static string FormatSyncOutcome(SyncHistoryEntity history)
    {
        var timestamp = (history.CompletedUtc ?? history.StartedUtc).ToLocalTime();
        var state = history.Status switch
        {
            "completed" => $"completed ({history.CharacterCount} characters, {history.ModCount} mods)",
            "cancelled" => "cancelled",
            "failed" => $"failed: {history.ErrorSummary ?? "unknown error"}",
            _ => "still in progress or interrupted"
        };
        var warnings = history.WarningCount > 0
            ? $" {history.WarningCount} parser warning(s)."
            : string.Empty;
        return $"{state} at {timestamp:yyyy-MM-dd HH:mm} local.{warnings}";
    }

    private static string FormatRecentSyncHistory(IReadOnlyList<SyncHistoryEntity> history)
    {
        if (history.Count == 0)
        {
            return "No sync attempts are recorded.";
        }

        return string.Join(
            Environment.NewLine,
            history.Select(entry =>
            {
                var timestamp = (entry.CompletedUtc ?? entry.StartedUtc).ToLocalTime();
                var state = entry.Status switch
                {
                    "completed" => $"completed ({entry.CharacterCount} characters, {entry.ModCount} mods)",
                    "cancelled" => "cancelled",
                    "failed" => $"failed: {entry.ErrorSummary ?? "unknown error"}",
                    _ => "still in progress or interrupted"
                };
                var warnings = entry.WarningCount > 0
                    ? $", {entry.WarningCount} warning(s)"
                    : string.Empty;
                return $"{timestamp:yyyy-MM-dd HH:mm} local - {RedactAllyCode(entry.AllyCode)} - {state}{warnings}";
            }));
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
