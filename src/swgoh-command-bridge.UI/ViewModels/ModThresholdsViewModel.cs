#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;

namespace swgoh_command_bridge.UI.ViewModels;

public class ModThresholdsViewModel : StateViewModelBase<IReadOnlyList<ModUpgradeThreshold>>
{
    private readonly ISettingsService _settingsService;
    private readonly ModThresholdTransferService _transferService = new();
    private string _headerText = "Manage Upgrade Rules & Thresholds";
    private ModUpgradeThreshold? _selectedThreshold;
    private string _name = string.Empty;
    private int _minimumRarity = 5;
    private int _minimumTier = 4;
    private int _minimumSpeed = 10;
    private bool _upgradeOnlyWithSpeed = true;
    private double _minimumEfficiency;
    private string _validationError = string.Empty;
    private string _transferPath = string.Empty;
    private string _transferStatusText = string.Empty;

    public ModThresholdsViewModel(ISettingsService settingsService)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        _settingsService = settingsService;
        RefreshCommand = new AsyncRelayCommand(LoadThresholdsAsync);
        AddCommand = new RelayCommand(AddThreshold);
        DuplicateCommand = new RelayCommand(DuplicateThreshold);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        SetDefaultCommand = new AsyncRelayCommand(SetDefaultAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
        ExportCommand = new AsyncRelayCommand(ExportAsync);
        ImportCommand = new AsyncRelayCommand(ImportAsync);
        _ = LoadThresholdsAsync();
    }

    public ObservableCollection<ModUpgradeThreshold> Thresholds { get; } = new();

    public string HeaderText
    {
        get => _headerText;
        set
        {
            if (_headerText == value)
            {
                return;
            }

            _headerText = value;
            OnPropertyChanged(nameof(HeaderText));
        }
    }

    public ModUpgradeThreshold? SelectedThreshold
    {
        get => _selectedThreshold;
        set
        {
            if (_selectedThreshold == value)
            {
                return;
            }

            _selectedThreshold = value;
            OnPropertyChanged(nameof(SelectedThreshold));
            OnPropertyChanged(nameof(IsDefault));
            OnPropertyChanged(nameof(DefaultStatusText));
            if (value != null)
            {
                Name = value.Name;
                MinimumRarity = value.MinimumRarity;
                MinimumTier = value.MinimumTier;
                MinimumSpeed = value.MinimumSpeed;
                UpgradeOnlyWithSpeed = value.UpgradeOnlyWithSpeed;
                MinimumEfficiency = value.MinimumEfficiency;
            }
        }
    }

    public string Name { get => _name; set => SetField(ref _name, value); }

    public int MinimumRarity { get => _minimumRarity; set => SetField(ref _minimumRarity, value); }

    public int MinimumTier { get => _minimumTier; set => SetField(ref _minimumTier, value); }

    public int MinimumSpeed { get => _minimumSpeed; set => SetField(ref _minimumSpeed, value); }

    public bool UpgradeOnlyWithSpeed { get => _upgradeOnlyWithSpeed; set => SetField(ref _upgradeOnlyWithSpeed, value); }

    public double MinimumEfficiency { get => _minimumEfficiency; set => SetField(ref _minimumEfficiency, value); }

    public bool IsDefault =>
        SelectedThreshold != null &&
        string.Equals(
            _settingsService.CurrentSettings.DefaultUpgradeThresholdId,
            SelectedThreshold.Id,
            StringComparison.Ordinal);

    public string DefaultStatusText => IsDefault
        ? "Active default threshold"
        : "Not the active default threshold";

    public string ValidationError
    {
        get => _validationError;
        private set
        {
            if (_validationError == value)
            {
                return;
            }

            _validationError = value;
            OnPropertyChanged(nameof(ValidationError));
            OnPropertyChanged(nameof(HasValidationError));
        }
    }

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationError);

    public string TransferPath
    {
        get => _transferPath;
        set => SetField(ref _transferPath, value);
    }

    public string TransferStatusText
    {
        get => _transferStatusText;
        private set => SetField(ref _transferStatusText, value);
    }

    public bool HasThresholds => State.Status == OperationStatus.Success;

    protected override void OnStateChanged() =>
        OnPropertyChanged(nameof(HasThresholds));

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand AddCommand { get; }

    public IRelayCommand DuplicateCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand SetDefaultCommand { get; }

    public IAsyncRelayCommand DeleteCommand { get; }

    public IAsyncRelayCommand ExportCommand { get; }

    public IAsyncRelayCommand ImportCommand { get; }

    public async Task LoadThresholdsAsync()
    {
        State = OperationState<IReadOnlyList<ModUpgradeThreshold>>.ToLoading();
        try
        {
            var settings = _settingsService.CurrentSettings.UpgradeThresholds ?? new List<ModUpgradeThresholdSetting>();
            var thresholds = settings.Select((setting, index) => new ModUpgradeThreshold(
                string.IsNullOrWhiteSpace(setting.Id) ? $"threshold-{index}" : setting.Id,
                string.IsNullOrWhiteSpace(setting.Name) ? $"Threshold {index + 1}" : setting.Name,
                Math.Clamp(setting.MinPips, 1, 6),
                Math.Clamp(setting.MinTier, 1, 5),
                string.Equals(setting.StatName, "Speed", StringComparison.OrdinalIgnoreCase)
                    ? Math.Max(0, (int)setting.MinValue)
                    : 0,
                setting.UpgradeOnlyWithSpeed,
                Math.Clamp(setting.MinimumEfficiency, 0, 100))).ToList();

            Thresholds.Clear();
            foreach (var threshold in thresholds)
            {
                Thresholds.Add(threshold);
            }

            var defaultThresholdId = _settingsService.CurrentSettings.DefaultUpgradeThresholdId;
            SelectedThreshold = Thresholds.FirstOrDefault(threshold =>
                string.Equals(threshold.Id, defaultThresholdId, StringComparison.Ordinal))
                ?? Thresholds.FirstOrDefault();
            ValidationError = string.Empty;
            State = thresholds.Count == 0
                ? OperationState<IReadOnlyList<ModUpgradeThreshold>>.ToEmpty()
                : OperationState<IReadOnlyList<ModUpgradeThreshold>>.ToSuccess(thresholds);
        }
        catch (Exception ex)
        {
            State = OperationState<IReadOnlyList<ModUpgradeThreshold>>.ToError(
                $"Failed to load thresholds: {ex.Message}");
        }
    }

    public void AddThreshold()
    {
        SelectedThreshold = null;
        Name = "New threshold";
        MinimumRarity = 5;
        MinimumTier = 4;
        MinimumSpeed = 10;
        UpgradeOnlyWithSpeed = true;
        MinimumEfficiency = 0;
        ValidationError = string.Empty;
    }

    public void DuplicateThreshold()
    {
        if (SelectedThreshold == null)
        {
            return;
        }

        var source = SelectedThreshold;
        SelectedThreshold = null;
        Name = $"{source.Name} Copy";
        MinimumRarity = source.MinimumRarity;
        MinimumTier = source.MinimumTier;
        MinimumSpeed = source.MinimumSpeed;
        UpgradeOnlyWithSpeed = source.UpgradeOnlyWithSpeed;
        MinimumEfficiency = source.MinimumEfficiency;
        ValidationError = string.Empty;
    }

    public async Task SaveAsync()
    {
        ValidationError = ValidateFields();
        if (HasValidationError)
        {
            return;
        }

        var previousThresholds = Thresholds.ToList();
        var previousSelectedThreshold = SelectedThreshold;
        var updated = new ModUpgradeThreshold(
            SelectedThreshold?.Id ?? Guid.NewGuid().ToString("N"),
            Name.Trim(),
            MinimumRarity,
            MinimumTier,
            MinimumSpeed,
            UpgradeOnlyWithSpeed,
            MinimumEfficiency);

        var index = SelectedThreshold == null
            ? -1
            : Thresholds.IndexOf(SelectedThreshold);
        if (index >= 0)
        {
            Thresholds[index] = updated;
        }
        else
        {
            Thresholds.Add(updated);
        }

        SelectedThreshold = updated;
        try
        {
            State = OperationState<IReadOnlyList<ModUpgradeThreshold>>.ToLoading();
            await PersistAsync();
        }
        catch (Exception ex)
        {
            Thresholds.Clear();
            foreach (var threshold in previousThresholds)
            {
                Thresholds.Add(threshold);
            }

            SelectedThreshold = previousSelectedThreshold;
            State = OperationState<IReadOnlyList<ModUpgradeThreshold>>.ToError(
                $"Failed to save threshold: {ex.Message}");
        }
    }

    public async Task SetDefaultAsync()
    {
        if (SelectedThreshold == null)
        {
            return;
        }

        try
        {
            State = OperationState<IReadOnlyList<ModUpgradeThreshold>>.ToLoading();
            await PersistAsync(SelectedThreshold.Id);
            OnPropertyChanged(nameof(IsDefault));
            OnPropertyChanged(nameof(DefaultStatusText));
        }
        catch (Exception ex)
        {
            State = OperationState<IReadOnlyList<ModUpgradeThreshold>>.ToError(
                $"Failed to set default threshold: {ex.Message}");
        }
    }

    public async Task DeleteAsync()
    {
        if (SelectedThreshold == null)
        {
            return;
        }

        var removedThreshold = SelectedThreshold;
        var removedIndex = Thresholds.IndexOf(removedThreshold);
        Thresholds.Remove(removedThreshold);
        SelectedThreshold = Thresholds.FirstOrDefault();
        try
        {
            State = OperationState<IReadOnlyList<ModUpgradeThreshold>>.ToLoading();
            await PersistAsync();
        }
        catch (Exception ex)
        {
            Thresholds.Insert(Math.Clamp(removedIndex, 0, Thresholds.Count), removedThreshold);
            SelectedThreshold = removedThreshold;
            State = OperationState<IReadOnlyList<ModUpgradeThreshold>>.ToError(
                $"Failed to delete threshold: {ex.Message}");
        }
    }

    public async Task ExportAsync()
    {
        TransferStatusText = string.Empty;
        if (string.IsNullOrWhiteSpace(TransferPath))
        {
            TransferStatusText = "Choose a JSON file path before exporting thresholds.";
            return;
        }

        try
        {
            var path = GetTransferPath();
            var json = _transferService.Serialize(Thresholds);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await WriteTransferFileAsync(path, json);
            TransferStatusText = $"Exported {Thresholds.Count} threshold(s) to {path}.";
        }
        catch (Exception ex)
        {
            TransferStatusText = $"Failed to export thresholds: {ex.Message}";
        }
    }

    public async Task ImportAsync()
    {
        TransferStatusText = string.Empty;
        if (string.IsNullOrWhiteSpace(TransferPath))
        {
            TransferStatusText = "Choose a JSON file path before importing thresholds.";
            return;
        }

        try
        {
            var path = GetTransferPath();
            var json = await File.ReadAllTextAsync(path);
            var importedThresholds = _transferService.DeserializeAndValidate(json);
            var defaultId = importedThresholds.FirstOrDefault()?.Id;

            await SaveThresholdsAsync(importedThresholds, defaultId);
            Thresholds.Clear();
            foreach (var threshold in importedThresholds)
            {
                Thresholds.Add(threshold);
            }

            SelectedThreshold = Thresholds.FirstOrDefault();
            ValidationError = string.Empty;
            State = importedThresholds.Count == 0
                ? OperationState<IReadOnlyList<ModUpgradeThreshold>>.ToEmpty()
                : OperationState<IReadOnlyList<ModUpgradeThreshold>>.ToSuccess(importedThresholds);
            TransferStatusText = $"Imported {importedThresholds.Count} threshold(s) from {path}.";
        }
        catch (Exception ex)
        {
            TransferStatusText = $"Failed to import thresholds: {ex.Message}";
        }
    }

    private async Task PersistAsync(string? preferredDefaultId = null)
    {
        await SaveThresholdsAsync(Thresholds, preferredDefaultId);

        var snapshot = Thresholds.ToList();
        State = snapshot.Count == 0
            ? OperationState<IReadOnlyList<ModUpgradeThreshold>>.ToEmpty()
            : OperationState<IReadOnlyList<ModUpgradeThreshold>>.ToSuccess(snapshot);
        OnPropertyChanged(nameof(IsDefault));
        OnPropertyChanged(nameof(DefaultStatusText));
    }

    private async Task SaveThresholdsAsync(
        IReadOnlyList<ModUpgradeThreshold> thresholds,
        string? preferredDefaultId = null)
    {
        var settings = thresholds.Select(ToSetting).ToList();
        var currentDefaultId = preferredDefaultId ?? _settingsService.CurrentSettings.DefaultUpgradeThresholdId;
        var defaultThresholdId = thresholds.Any(threshold =>
            string.Equals(threshold.Id, currentDefaultId, StringComparison.Ordinal))
            ? currentDefaultId
            : thresholds.FirstOrDefault()?.Id;

        await _settingsService.SaveSettingsAsync(
            _settingsService.CurrentSettings with
            {
                UpgradeThresholds = settings,
                DefaultUpgradeThresholdId = defaultThresholdId
            });
    }

    private static ModUpgradeThresholdSetting ToSetting(ModUpgradeThreshold threshold) =>
        new(
            threshold.MinimumRarity,
            threshold.MinimumTier,
            "Speed",
            threshold.MinimumSpeed,
            threshold.Name,
            threshold.UpgradeOnlyWithSpeed,
            threshold.MinimumEfficiency,
            threshold.Id);

    private string GetTransferPath()
    {
        var path = Path.GetFullPath(TransferPath.Trim());
        if (string.IsNullOrWhiteSpace(Path.GetFileName(path)))
        {
            throw new InvalidDataException("The transfer path must include a file name.");
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

    private string ValidateFields()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return "A threshold name is required.";
        }

        if (MinimumRarity is < 1 or > 6)
        {
            return "Minimum pips must be between 1 and 6.";
        }

        if (MinimumTier is < 1 or > 5)
        {
            return "Minimum tier must be between 1 and 5.";
        }

        if (MinimumSpeed < 0)
        {
            return "Minimum speed cannot be negative.";
        }

        if (UpgradeOnlyWithSpeed && MinimumSpeed == 0)
        {
            return "A speed minimum is required when Require speed is enabled.";
        }

        if (double.IsNaN(MinimumEfficiency) ||
            double.IsInfinity(MinimumEfficiency) ||
            MinimumEfficiency is < 0 or > 100)
        {
            return "Minimum efficiency must be between 0 and 100.";
        }

        return string.Empty;
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
