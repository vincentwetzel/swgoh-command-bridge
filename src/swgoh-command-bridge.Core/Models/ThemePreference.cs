#nullable enable

namespace swgoh_command_bridge.Core.Models;

/// <summary>Supported application theme preferences.</summary>
public static class ThemePreference
{
    public const string Dark = "Dark";
    public const string Light = "Light";
    public const string System = "System";

    public static string Normalize(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "light" => Light,
            "system" or "default" => System,
            _ => Dark
        };
}
