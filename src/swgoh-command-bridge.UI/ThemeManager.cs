#nullable enable

using Avalonia;
using Avalonia.Styling;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.UI;

/// <summary>Applies the persisted theme preference to the active Avalonia application.</summary>
public static class ThemeManager
{
    public static void Apply(string? preference)
    {
        if (Application.Current == null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = ThemePreference.Normalize(preference) switch
        {
            ThemePreference.Light => ThemeVariant.Light,
            ThemePreference.System => ThemeVariant.Default,
            _ => ThemeVariant.Dark
        };
    }
}
