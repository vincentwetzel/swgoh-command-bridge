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
    public async Task SaveCommandPersistsRecommendationContactEmail()
    {
        var settings = new FakeSettingsService();
        var viewModel = CreateViewModel(settings);
        viewModel.RecommendationContactEmail = "operator@example.com";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal("operator@example.com", settings.CurrentSettings.RecommendationContactEmail);
    }

    [Fact]
    public async Task SaveCommandNormalizesAndAppliesTheme()
    {
        var settings = new FakeSettingsService();
        var appliedTheme = string.Empty;
        var viewModel = new SettingsViewModel(
            settings,
            _ => { },
            _ => { },
            () => Task.CompletedTask,
            applyTheme: theme => appliedTheme = theme);
        viewModel.Theme = "light";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Light", settings.CurrentSettings.Theme);
        Assert.Equal("Light", appliedTheme);
        Assert.Equal("Light", viewModel.Theme);
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
    public async Task ImportCommandNormalizesUnsupportedThemeToDarkAndAppliesIt()
    {
        var settings = new FakeSettingsService();
        var appliedTheme = string.Empty;
        var viewModel = new SettingsViewModel(
            settings,
            _ => { },
            _ => { },
            () => Task.CompletedTask,
            applyTheme: theme => appliedTheme = theme);
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "theme.json");

        try
        {
            await File.WriteAllTextAsync(
                path,
                "{\"schemaVersion\":1,\"settings\":{\"comlinkBaseUrl\":\"http://localhost:4000\",\"theme\":\"Solarized\"}}");
            viewModel.SettingsTransferPath = path;

            await viewModel.ImportSettingsCommand.ExecuteAsync(null);

            Assert.Equal("Dark", settings.CurrentSettings.Theme);
            Assert.Equal("Dark", appliedTheme);
            Assert.Equal("Dark", viewModel.Theme);
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

    [Fact]
    public async Task BackupCacheCommandSurfacesCallbackFailureAsRetryableError()
    {
        var settings = new FakeSettingsService();
        var viewModel = new SettingsViewModel(
            settings,
            _ => { },
            _ => { },
            () => Task.CompletedTask,
            backupCache: () => Task.FromException<string>(new IOException("backup unavailable")));

        await viewModel.BackupCacheCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Contains("back up", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("backup unavailable", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RestoreCacheCommandSurfacesCallbackFailureAndRetainsConfirmation()
    {
        var settings = new FakeSettingsService();
        var viewModel = new SettingsViewModel(
            settings,
            _ => { },
            _ => { },
            () => Task.CompletedTask,
            restoreCache: _ => Task.FromException(new IOException("restore unavailable")));
        viewModel.ConfirmCacheRestore = true;
        viewModel.RestoreBackupPath = "cache-backup.db";

        await viewModel.RestoreCacheCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.True(viewModel.ConfirmCacheRestore);
        Assert.Contains("restore unavailable", viewModel.RestoreStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("restore", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveCommandSurfacesSettingsPersistenceFailure()
    {
        var settings = new FakeSettingsService { ThrowOnSave = true };
        var viewModel = CreateViewModel(settings);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Contains("save settings", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(settings.SavedSettings);
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

        public bool ThrowOnSave { get; set; }

        public Task LoadSettingsAsync() => Task.CompletedTask;

        public Task SaveSettingsAsync(AppSettings settings)
        {
            if (ThrowOnSave)
            {
                throw new IOException("settings persistence unavailable");
            }

            CurrentSettings = settings;
            SavedSettings.Add(settings);
            return Task.CompletedTask;
        }
    }
}
