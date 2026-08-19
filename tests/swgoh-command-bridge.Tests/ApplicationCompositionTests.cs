#nullable enable

using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using swgoh_command_bridge.UI;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class ApplicationCompositionTests
{
    [Fact]
    public async Task CreateDefault_WiresSharedServicesAndKeepsInjectedDatabaseOwnershipWithCaller()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var settings = new TestSettingsService(new AppSettings(ComlinkBaseUrl: "http://localhost:4321"));

        var composition = ApplicationComposition.CreateDefault(context, settings);

        Assert.Same(context, composition.Database);
        Assert.Same(settings, composition.Settings);
        Assert.NotNull(composition.ComlinkService);
        Assert.NotNull(composition.PreferredModsService);
        Assert.NotNull(composition.PlayerService);
        Assert.NotNull(composition.PlayerRepository);
        Assert.NotNull(composition.AdvisorService);
        Assert.NotNull(composition.AssignmentService);
        Assert.NotNull(composition.ScraperService);
        Assert.Equal("http://localhost:4321/", composition.ComlinkClient.BaseAddress!.ToString());

        composition.Dispose();
        composition.Dispose();

        Assert.True(await context.Database.CanConnectAsync());
    }

    [Fact]
    public void CreateDefault_UsesSafeLocalComlinkEndpointForInvalidSettings()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = new AppDbContext(options);
        var composition = ApplicationComposition.CreateDefault(
            context,
            new TestSettingsService(new AppSettings(ComlinkBaseUrl: "not-a-url")));

        Assert.Equal("http://localhost:3000/", composition.ComlinkClient.BaseAddress!.ToString());
        composition.Dispose();
    }

    private sealed class TestSettingsService : ISettingsService
    {
        public TestSettingsService(AppSettings settings)
        {
            CurrentSettings = settings;
        }

        public AppSettings CurrentSettings { get; private set; }

        public string SettingsPath => "composition-settings.json";

        public string DiagnosticsDirectory => "composition-diagnostics";

        public Task LoadSettingsAsync() => Task.CompletedTask;

        public Task SaveSettingsAsync(AppSettings settings)
        {
            CurrentSettings = settings;
            return Task.CompletedTask;
        }
    }
}
