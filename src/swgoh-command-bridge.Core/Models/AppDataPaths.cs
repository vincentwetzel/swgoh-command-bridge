#nullable enable

using System;
using System.IO;

namespace swgoh_command_bridge.Core.Models;

/// <summary>
/// Defines the persistent application-data locations shared by the cache and settings services.
/// </summary>
public static class AppDataPaths
{
    public const string ApplicationDirectoryName = "SWGOHCommandBridge";

    public static string ApplicationDirectory =>
        Path.Combine(GetLocalApplicationDataRoot(), ApplicationDirectoryName);

    public static string CachePath => Path.Combine(ApplicationDirectory, "cache.db");

    public static string SettingsPath => Path.Combine(ApplicationDirectory, "settings.json");

    public static string DiagnosticsDirectory => Path.Combine(ApplicationDirectory, "diagnostics");

    public static string CharacterCatalogDirectory => Path.Combine(ApplicationDirectory, "character-catalog");

    public static string ComlinkDirectory => Path.Combine(ApplicationDirectory, "comlink");

    private static string GetLocalApplicationDataRoot()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localApplicationData))
        {
            return localApplicationData;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(userProfile)
            ? AppContext.BaseDirectory
            : Path.Combine(userProfile, ".local", "share");
    }
}
