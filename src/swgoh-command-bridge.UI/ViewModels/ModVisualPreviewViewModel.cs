#nullable enable

using System.Collections.Generic;
using System.Collections.ObjectModel;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.UI.Controls;

namespace swgoh_command_bridge.UI.ViewModels;

public sealed class ModVisualPreviewViewModel : ViewModelBase
{
    public ModVisualPreviewViewModel()
    {
        var normal = new[]
        {
            (ModSlot.Square, ModSet.Health, 1, 1),
            (ModSlot.Arrow, ModSet.Offense, 2, 3),
            (ModSlot.Diamond, ModSet.Defense, 3, 5),
            (ModSlot.Triangle, ModSet.CriticalChance, 4, 8),
            (ModSlot.Circle, ModSet.Tenacity, 5, 12),
            (ModSlot.Cross, ModSet.Speed, 5, 15)
        };
        var sixDot = new[]
        {
            (ModSlot.Square, ModSet.Potency, 6, 15),
            (ModSlot.Arrow, ModSet.Speed, 7, 15),
            (ModSlot.Diamond, ModSet.CriticalDamage, 6, 10),
            (ModSlot.Triangle, ModSet.Health, 6, 12),
            (ModSlot.Circle, ModSet.Offense, 6, 7),
            (ModSlot.Cross, ModSet.Defense, 6, 15)
        };

        foreach (var sample in normal)
        {
            Samples.Add(new ModVisualPreviewSample(
                $"Normal · {sample.Item1} · {sample.Item2} · {sample.Item3} dots · Lv {sample.Item4}",
                new ModVisualRequest(sample.Item1, sample.Item2, sample.Item3, sample.Item4)));
        }

        foreach (var sample in sixDot)
        {
                Samples.Add(new ModVisualPreviewSample(
                    $"6-dot · {sample.Item1} · {sample.Item2} · {sample.Item3} dots · Lv {sample.Item4}",
                new ModVisualRequest(sample.Item1, sample.Item2, sample.Item3, sample.Item4)));
        }
    }

    public ObservableCollection<ModVisualPreviewSample> Samples { get; } = new();
}

public sealed record ModVisualPreviewSample(string Label, ModVisualRequest Request);
