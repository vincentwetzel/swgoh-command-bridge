#nullable enable

using System;
using System.Collections.Generic;
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
        private readonly string _settingsDirectory;

        public SettingsServiceTests()
        {
            _settingsDirectory = Path.Combine(
                Path.GetTempPath(),
                "swgoh-command-bridge-settings-tests",
                Guid.NewGuid().ToString("N"));
        }

        [Fact]
        public async Task LoadSettingsAsync_WhenFileDoesNotExist_LoadsDefaultSettings()
        {
            // Arrange
            if (File.Exists(_settingsFilePath))
            {
                File.Delete(_settingsFilePath);
            }
            var service = new SettingsService(
                NullLogger<SettingsService>.Instance,
                _settingsDirectory);

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
            var service = new SettingsService(
                NullLogger<SettingsService>.Instance,
                _settingsDirectory);
            var customSettings = new AppSettings(
                ComlinkBaseUrl: "http://192.168.1.50:3000",
                DefaultAllyCode: "111222333",
                Theme: "Light",
                AutomaticallyCheckForUpdates: false,
                UpgradeThresholds: new List<ModUpgradeThresholdSetting>
                {
                    new(5, 4, "Speed", 10, "Competitive", true, 65, "competitive-threshold")
                },
                DefaultUpgradeThresholdId: "competitive-threshold"
            );

            // Act
            await service.SaveSettingsAsync(customSettings);

            // Force a reload on a new service instance to ensure persistence
            var readerService = new SettingsService(
                NullLogger<SettingsService>.Instance,
                _settingsDirectory);
            await readerService.LoadSettingsAsync();

            // Assert
            Assert.Equal("http://192.168.1.50:3000", readerService.CurrentSettings.ComlinkBaseUrl);
            Assert.Equal("111222333", readerService.CurrentSettings.DefaultAllyCode);
            Assert.Equal("Light", readerService.CurrentSettings.Theme);
            Assert.False(readerService.CurrentSettings.AutomaticallyCheckForUpdates);
            var threshold = Assert.Single(readerService.CurrentSettings.UpgradeThresholds!);
            Assert.Equal("Competitive", threshold.Name);
            Assert.Equal(65, threshold.MinimumEfficiency);
            Assert.Equal("competitive-threshold", threshold.Id);
            Assert.Equal("competitive-threshold", readerService.CurrentSettings.DefaultUpgradeThresholdId);
        }

        public void Dispose()
        {
            if (Directory.Exists(_settingsDirectory))
            {
                Directory.Delete(_settingsDirectory, recursive: true);
            }
        }
    }
}
