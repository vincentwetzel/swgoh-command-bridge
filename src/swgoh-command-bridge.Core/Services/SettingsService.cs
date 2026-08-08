#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services
{
    /// <summary>
    /// Manages application configuration and settings using atomic writes and cross-platform paths.
    /// </summary>
    public class SettingsService : ISettingsService
    {
        public const int CurrentSchemaVersion = 1;

        private sealed record PersistedSettingsDocument(int SchemaVersion, AppSettings Settings);

        private readonly ILogger<SettingsService> _logger;
        private readonly string _settingsDirectory;
        private readonly string _settingsFilePath;
        private readonly string _tempFilePath;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private AppSettings _currentSettings = new();

        /// <inheritdoc />
        public AppSettings CurrentSettings => _currentSettings;

        public string SettingsPath => _settingsFilePath;

        public string DiagnosticsDirectory => AppDataPaths.DiagnosticsDirectory;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsService"/> class.
        /// </summary>
        public SettingsService(ILogger<SettingsService> logger, string? settingsDirectory = null)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;

            _settingsDirectory = string.IsNullOrWhiteSpace(settingsDirectory)
                ? AppDataPaths.ApplicationDirectory
                : Path.GetFullPath(settingsDirectory);
            _settingsFilePath = Path.Combine(_settingsDirectory, "settings.json");
            _tempFilePath = Path.Combine(_settingsDirectory, "settings.json.tmp");
        }

        /// <inheritdoc />
        public async Task LoadSettingsAsync()
        {
            _logger.LogInformation("Loading application settings from {FilePath}", _settingsFilePath);

            if (!File.Exists(_settingsFilePath))
            {
                _logger.LogInformation("Settings file does not exist. Initializing with defaults.");
                _currentSettings = new AppSettings();
                return;
            }

            try
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath).ConfigureAwait(false);
                var document = JsonSerializer.Deserialize<PersistedSettingsDocument>(json, SerializerOptions);
                if (document?.Settings != null)
                {
                    if (document.SchemaVersion > CurrentSchemaVersion)
                    {
                        throw new InvalidDataException(
                            $"The settings schema version {document.SchemaVersion} is newer than supported version {CurrentSchemaVersion}.");
                    }

                    _currentSettings = SettingsMigrationService.MigrateLegacyThresholdStorage(document.Settings);
                    if (document.SchemaVersion != CurrentSchemaVersion)
                    {
                        await SaveSettingsAsync(_currentSettings).ConfigureAwait(false);
                    }

                    return;
                }

                var legacySettings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
                _currentSettings = SettingsMigrationService.MigrateLegacyThresholdStorage(
                    legacySettings ?? new AppSettings());
                await SaveSettingsAsync(_currentSettings).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load settings from {FilePath}. Falling back to defaults.", _settingsFilePath);
                _currentSettings = new AppSettings();
            }
        }

        /// <inheritdoc />
        public async Task SaveSettingsAsync(AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            _logger.LogInformation("Saving settings atomically to {FilePath}", _settingsFilePath);

            try
            {
                // Ensure the directory exists
                if (!Directory.Exists(_settingsDirectory))
                {
                    Directory.CreateDirectory(_settingsDirectory);
                }

                // Rule 26: 1. Serialize configuration output to a temporary staging file
                using (var stream = File.Create(_tempFilePath))
                {
                    await JsonSerializer.SerializeAsync(
                        stream,
                        new PersistedSettingsDocument(CurrentSchemaVersion, settings),
                        SerializerOptions).ConfigureAwait(false);
                }

                // Rule 26: 3. Atomically overwrite the active settings file using File.Move with overwrite
                File.Move(_tempFilePath, _settingsFilePath, overwrite: true);

                _currentSettings = settings;
                _logger.LogInformation("Successfully saved settings atomically.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to atomically write settings to {FilePath}", _settingsFilePath);

                // Cleanup temp file if it still exists
                if (File.Exists(_tempFilePath))
                {
                    try
                    {
                        File.Delete(_tempFilePath);
                    }
                    catch (Exception delEx)
                    {
                        _logger.LogWarning(delEx, "Failed to delete temporary settings staging file {TempPath}", _tempFilePath);
                    }
                }
                throw;
            }
        }
    }
}
