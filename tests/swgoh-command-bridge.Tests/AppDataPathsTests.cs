#nullable enable

using System.IO;
using swgoh_command_bridge.Core.Models;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class AppDataPathsTests
{
    [Fact]
    public void CacheSettingsAndDiagnosticsShareOneCaseStableApplicationDirectory()
    {
        var applicationDirectory = Path.GetDirectoryName(AppDataPaths.CachePath);

        Assert.NotNull(applicationDirectory);
        Assert.Equal(applicationDirectory, Path.GetDirectoryName(AppDataPaths.SettingsPath));
        Assert.Equal(applicationDirectory, Path.GetDirectoryName(AppDataPaths.DiagnosticsDirectory));
        Assert.Equal(applicationDirectory, Path.GetDirectoryName(AppDataPaths.PreferredModsDirectory));
        Assert.Equal(AppDataPaths.ApplicationDirectoryName, new DirectoryInfo(applicationDirectory!).Name);
        Assert.EndsWith("cache.db", AppDataPaths.CachePath, System.StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("settings.json", AppDataPaths.SettingsPath, System.StringComparison.OrdinalIgnoreCase);
    }
}
