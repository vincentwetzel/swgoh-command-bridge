#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using swgoh_command_bridge.UI.ViewModels;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task SaveCommand_RejectsNonHttpComlinkUrl()
    {
        var settings = new FakeSettingsService();
        var viewModel = CreateViewModel(settings);
        viewModel.ComlinkBaseUrl = "ftp://localhost:3000";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Contains("HTTP or HTTPS", viewModel.StatusText);
        Assert.Empty(settings.SavedSettings);
    }

    [Fact]
    public async Task SaveCommandPersistsLocalRecommendationScrapingPolicy()
    {
        var settings = new FakeSettingsService();
        var viewModel = CreateViewModel(settings);
        viewModel.EnableLocalRecommendationScraping = false;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.False(settings.CurrentSettings.EnableLocalRecommendationScraping);
    }

    [Fact]
    public async Task ExportCommand_WritesSecretSafePortableSettings()
    {
        var settings = new FakeSettingsService
        {
            CurrentSettings = new AppSettings(
                ComlinkBaseUrl: "http://user:secret@localhost:3000",
                DefaultAllyCode: "123456789")
        };
        var viewModel = CreateViewModel(settings);
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "settings.json");

        try
        {
            viewModel.SettingsTransferPath = path;
            await viewModel.ExportSettingsCommand.ExecuteAsync(null);

            var contents = await File.ReadAllTextAsync(path);
            Assert.Contains("schemaVersion", contents);
            Assert.DoesNotContain("secret", contents);
            Assert.Contains("Exported settings", viewModel.SettingsTransferStatusText);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ImportCommand_SavesSettingsAppliesRuntimeValuesAndRefreshesScope()
    {
        var settings = new FakeSettingsService();
        var appliedUrl = string.Empty;
        var appliedAllyCode = string.Empty;
        var refreshCount = 0;
        var viewModel = new SettingsViewModel(
            settings,
            url => appliedUrl = url,
            allyCode => appliedAllyCode = allyCode,
            () => Task.CompletedTask,
            refreshAfterImport: () =>
            {
                refreshCount++;
                return Task.CompletedTask;
            });
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "import.json");

        try
        {
            await File.WriteAllTextAsync(
                path,
                "{\"schemaVersion\":1,\"settings\":{\"comlinkBaseUrl\":\"http://localhost:4000\",\"defaultAllyCode\":\"987654321\",\"theme\":\"Light\"}}");
            viewModel.SettingsTransferPath = path;

            await viewModel.ImportSettingsCommand.ExecuteAsync(null);

            Assert.Equal("http://localhost:4000/", appliedUrl);
            Assert.Equal("987654321", appliedAllyCode);
            Assert.Equal(1, refreshCount);
            Assert.Equal("987654321", settings.CurrentSettings.DefaultAllyCode);
            Assert.Contains("refreshed", viewModel.SettingsTransferStatusText);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ResetCacheCommandRequiresConfirmationThenInvokesCallback()
    {
        var settings = new FakeSettingsService();
        var resetCount = 0;
        var viewModel = new SettingsViewModel(
            settings,
            _ => { },
            _ => { },
            () => Task.CompletedTask,
            resetCache: () =>
            {
                resetCount++;
                return Task.CompletedTask;
            });

        await viewModel.ResetCacheCommand.ExecuteAsync(null);
        Assert.Equal(0, resetCount);
        Assert.True(viewModel.HasError);

        viewModel.ConfirmCacheReset = true;
        await viewModel.ResetCacheCommand.ExecuteAsync(null);

        Assert.Equal(1, resetCount);
        Assert.False(viewModel.ConfirmCacheReset);
        Assert.False(viewModel.HasError);
    }

    private static SettingsViewModel CreateViewModel(FakeSettingsService settings) =>
        new(
            settings,
            _ => { },
            _ => { },
            () => Task.CompletedTask);

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "swgoh-command-bridge-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings CurrentSettings { get; set; } = new();

        public string SettingsPath => "test-settings.json";

        public string DiagnosticsDirectory => Path.GetTempPath();

        public System.Collections.Generic.List<AppSettings> SavedSettings { get; } = new();

        public Task LoadSettingsAsync() => Task.CompletedTask;

        public Task SaveSettingsAsync(AppSettings settings)
        {
            CurrentSettings = settings;
            SavedSettings.Add(settings);
            return Task.CompletedTask;
        }
    }
}
