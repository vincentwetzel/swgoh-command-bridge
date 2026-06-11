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

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsService"/> class.
        /// </summary>
        public SettingsService(ILogger<SettingsService> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;

            // Rule 16: Cross-platform user settings directory in LocalApplicationData
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _settingsDirectory = Path.Combine(appData, "SWGOHCommandBridge");
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
                using var stream = File.OpenRead(_settingsFilePath);
                var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions).ConfigureAwait(false);
                _currentSettings = settings ?? new AppSettings();
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
                    await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions).ConfigureAwait(false);
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