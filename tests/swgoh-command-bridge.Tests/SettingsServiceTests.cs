#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests
{
    /// <summary>
    /// Integration tests for the AppSettings serialization and persistence cycle via SettingsService.
    /// </summary>
    public class SettingsServiceTests : IDisposable
    {
        private readonly string _appData;
        private readonly string _settingsDirectory;
        private readonly string _settingsFilePath;
        private readonly string _backupFilePath;

        public SettingsServiceTests()
        {
            _appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _settingsDirectory = Path.Combine(_appData, "SWGOHCommandBridge");
            _settingsFilePath = Path.Combine(_settingsDirectory, "settings.json");
            _backupFilePath = Path.Combine(_settingsDirectory, "settings.json.bak_test");

            // Safe backup of any existing live settings
            if (File.Exists(_settingsFilePath))
            {
                if (!Directory.Exists(_settingsDirectory))
                {
                    Directory.CreateDirectory(_settingsDirectory);
                }
                File.Copy(_settingsFilePath, _backupFilePath, overwrite: true);
                File.Delete(_settingsFilePath);
            }
        }

        [Fact]
        public async Task LoadSettingsAsync_WhenFileDoesNotExist_LoadsDefaultSettings()
        {
            // Arrange
            if (File.Exists(_settingsFilePath))
            {
                File.Delete(_settingsFilePath);
            }
            var service = new SettingsService(NullLogger<SettingsService>.Instance);

            // Act
            await service.LoadSettingsAsync();

            // Assert
            Assert.NotNull(service.CurrentSettings);
            Assert.Equal("http://localhost:3000", service.CurrentSettings.ComlinkBaseUrl);
            Assert.Equal("Dark", service.CurrentSettings.Theme);
        }

        [Fact]
        public async Task SaveSettingsAsync_WhenInvoked_PersistsSettingsCorrectly()
        {
            // Arrange
            var service = new SettingsService(NullLogger<SettingsService>.Instance);
            var customSettings = new AppSettings(
                ComlinkBaseUrl: "http://192.168.1.50:3000",
                DefaultAllyCode: "111222333",
                Theme: "Light",
                AutomaticallyCheckForUpdates: false
            );

            // Act
            await service.SaveSettingsAsync(customSettings);

            // Force a reload on a new service instance to ensure persistence
            var readerService = new SettingsService(NullLogger<SettingsService>.Instance);
            await readerService.LoadSettingsAsync();

            // Assert
            Assert.Equal("http://192.168.1.50:3000", readerService.CurrentSettings.ComlinkBaseUrl);
            Assert.Equal("111222333", readerService.CurrentSettings.DefaultAllyCode);
            Assert.Equal("Light", readerService.CurrentSettings.Theme);
            Assert.False(readerService.CurrentSettings.AutomaticallyCheckForUpdates);
        }

        public void Dispose()
        {
            if (File.Exists(_settingsFilePath)) File.Delete(_settingsFilePath);
            if (File.Exists(_backupFilePath)) File.Copy(_backupFilePath, _settingsFilePath, overwrite: true);
        }
    }
}