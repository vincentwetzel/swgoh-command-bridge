#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using swgoh_command_bridge.UI.ViewModels;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class ModThresholdsViewModelTests
{
    [Fact]
    public async Task LoadThresholdsAsync_WithNoConfiguredThresholdsReportsEmptyState()
    {
        var viewModel = new ModThresholdsViewModel(new FakeSettingsService());

        await viewModel.LoadThresholdsAsync();

        Assert.Empty(viewModel.Thresholds);
        Assert.Equal(OperationStatus.Empty, viewModel.State.Status);
        Assert.True(viewModel.IsEmpty);
        Assert.False(viewModel.HasThresholds);
    }

    [Fact]
    public async Task LoadThresholdsAsync_MapsSettingsAndSelectsConfiguredDefault()
    {
        var settings = new FakeSettingsService
        {
            CurrentSettings = new AppSettings(
                UpgradeThresholds: new List<ModUpgradeThresholdSetting>
                {
                    new(4, 2, "Speed", 8, "Lower", Id: "lower"),
                    new(6, 5, "Speed", 20, "Active", MinimumEfficiency: 70, Id: "active")
                },
                DefaultUpgradeThresholdId: "active")
        };
        var viewModel = new ModThresholdsViewModel(settings);

        await viewModel.LoadThresholdsAsync();

        Assert.Equal(OperationStatus.Success, viewModel.State.Status);
        Assert.Equal(2, viewModel.Thresholds.Count);
        Assert.Equal("active", viewModel.SelectedThreshold!.Id);
        Assert.Equal(6, viewModel.MinimumRarity);
        Assert.Equal(5, viewModel.MinimumTier);
        Assert.Equal(20, viewModel.MinimumSpeed);
        Assert.Equal(70d, viewModel.MinimumEfficiency);
        Assert.True(viewModel.IsDefault);
    }

    [Fact]
    public async Task CreateDuplicateSaveDefaultAndDelete_PersistFullLifecycle()
    {
        var settings = new FakeSettingsService
        {
            CurrentSettings = new AppSettings(
                UpgradeThresholds: new List<ModUpgradeThresholdSetting>
                {
                    new(5, 4, "Speed", 10, "Original", Id: "original")
                },
                DefaultUpgradeThresholdId: "original")
        };
        var viewModel = new ModThresholdsViewModel(settings);
        await viewModel.LoadThresholdsAsync();

        viewModel.DuplicateCommand.Execute(null);
        Assert.Equal("Original Copy", viewModel.Name);
        viewModel.MinimumEfficiency = 65;
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.Thresholds.Count);
        Assert.NotEqual("original", viewModel.SelectedThreshold!.Id);
        Assert.Equal(65d, settings.CurrentSettings.UpgradeThresholds![1].MinimumEfficiency);

        await viewModel.SetDefaultCommand.ExecuteAsync(null);
        Assert.Equal(viewModel.SelectedThreshold.Id, settings.CurrentSettings.DefaultUpgradeThresholdId);
        Assert.True(viewModel.IsDefault);

        await viewModel.DeleteCommand.ExecuteAsync(null);
        Assert.Single(viewModel.Thresholds);
        Assert.Equal(OperationStatus.Success, viewModel.State.Status);
        Assert.Equal("original", settings.CurrentSettings.DefaultUpgradeThresholdId);
    }

    [Fact]
    public async Task SaveCommand_RejectsInvalidFieldsWithoutChangingSettings()
    {
        var settings = new FakeSettingsService();
        var viewModel = new ModThresholdsViewModel(settings);
        await viewModel.LoadThresholdsAsync();
        viewModel.AddCommand.Execute(null);
        viewModel.Name = string.Empty;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasValidationError);
        Assert.Contains("name", viewModel.ValidationError, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(settings.SavedSettings);
        Assert.Empty(viewModel.Thresholds);
    }

    [Fact]
    public async Task SaveCommand_RejectsNonFiniteEfficiencyWithoutChangingSettings()
    {
        var settings = new FakeSettingsService();
        var viewModel = new ModThresholdsViewModel(settings);
        await viewModel.LoadThresholdsAsync();
        viewModel.AddCommand.Execute(null);
        viewModel.MinimumEfficiency = double.NaN;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasValidationError);
        Assert.Contains("efficiency", viewModel.ValidationError, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(settings.SavedSettings);
        Assert.Empty(viewModel.Thresholds);
    }

    [Fact]
    public async Task SaveCommand_RestoresInMemoryThresholdsWhenSettingsPersistenceFails()
    {
        var settings = new FakeSettingsService
        {
            CurrentSettings = new AppSettings(
                UpgradeThresholds: new List<ModUpgradeThresholdSetting>
                {
                    new(5, 4, "Speed", 10, "Original", Id: "original")
                },
                DefaultUpgradeThresholdId: "original"),
            ThrowOnSave = true
        };
        var viewModel = new ModThresholdsViewModel(settings);
        await viewModel.LoadThresholdsAsync();
        viewModel.Name = "Edited but rejected";

        await viewModel.SaveCommand.ExecuteAsync(null);

        var restored = Assert.Single(viewModel.Thresholds);
        Assert.Equal("original", restored.Id);
        Assert.Equal("Original", restored.Name);
        Assert.Equal(OperationStatus.Error, viewModel.State.Status);
        Assert.Contains("failed to save", viewModel.State.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportAndImportCommands_RoundTripVersionedThresholdData()
    {
        var settings = new FakeSettingsService
        {
            CurrentSettings = new AppSettings(
                UpgradeThresholds: new List<ModUpgradeThresholdSetting>
                {
                    new(5, 4, "Speed", 12, "Exported", Id: "exported")
                })
        };
        var viewModel = new ModThresholdsViewModel(settings);
        await viewModel.LoadThresholdsAsync();
        var directory = Path.Combine(Path.GetTempPath(), "swgoh-command-bridge-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "thresholds.json");

        try
        {
            viewModel.TransferPath = path;
            await viewModel.ExportCommand.ExecuteAsync(null);
            var contents = await File.ReadAllTextAsync(path);

            Assert.Contains("schemaVersion", contents);
            Assert.Contains("exported", contents);

            viewModel.AddCommand.Execute(null);
            await viewModel.SaveCommand.ExecuteAsync(null);
            Assert.Equal(2, viewModel.Thresholds.Count);

            await viewModel.ImportCommand.ExecuteAsync(null);

            Assert.Single(viewModel.Thresholds);
            Assert.Equal("exported", viewModel.SelectedThreshold!.Id);
            Assert.Contains("Imported 1", viewModel.TransferStatusText);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings CurrentSettings { get; set; } = new();

        public string SettingsPath => "threshold-test-settings.json";

        public string DiagnosticsDirectory => Path.GetTempPath();

        public List<AppSettings> SavedSettings { get; } = new();

        public bool ThrowOnSave { get; set; }

        public Task LoadSettingsAsync() => Task.CompletedTask;

        public Task SaveSettingsAsync(AppSettings settings)
        {
            if (ThrowOnSave)
            {
                throw new IOException("simulated settings write failure");
            }

            CurrentSettings = settings;
            SavedSettings.Add(settings);
            return Task.CompletedTask;
        }
    }
}
