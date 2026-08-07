#nullable enable

using System;
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Database.Repositories;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;

namespace swgoh_command_bridge.UI;

/// <summary>
/// Owns the application's long-lived services and their shared dependencies.
/// </summary>
public sealed class ApplicationComposition : IDisposable
{
    private readonly bool _ownsDatabase;
    private bool _disposed;

    private ApplicationComposition(
        AppDbContext database,
        ISettingsService settings,
        HttpClient comlinkClient,
        IComlinkService comlinkService,
        IPlayerService playerService,
        IModAdvisorService advisorService,
        IModAssignmentService assignmentService,
        ISwgohGgScraperService scraperService,
        bool ownsDatabase)
    {
        Database = database;
        Settings = settings;
        ComlinkClient = comlinkClient;
        ComlinkService = comlinkService;
        PlayerService = playerService;
        AdvisorService = advisorService;
        AssignmentService = assignmentService;
        ScraperService = scraperService;
        _ownsDatabase = ownsDatabase;
    }

    public AppDbContext Database { get; }

    public ISettingsService Settings { get; }

    public HttpClient ComlinkClient { get; }

    public IComlinkService ComlinkService { get; }

    public IPlayerService PlayerService { get; }

    public IModAdvisorService AdvisorService { get; }

    public IModAssignmentService AssignmentService { get; }

    public ISwgohGgScraperService ScraperService { get; }

    /// <summary>
    /// Creates the default desktop application service graph.
    /// </summary>
    public static ApplicationComposition CreateDefault(AppDbContext? database = null)
    {
        var resolvedDatabase = database ?? new AppDbContext();
        var settings = new SettingsService(NullLogger<SettingsService>.Instance);
        settings.LoadSettingsAsync().GetAwaiter().GetResult();

        var comlinkClient = new HttpClient
        {
            BaseAddress = new Uri(GetSafeComlinkUrl(settings.CurrentSettings.ComlinkBaseUrl), UriKind.Absolute)
        };
        var comlinkService = new ComlinkService(
            comlinkClient,
            NullLogger<ComlinkService>.Instance);
        var playerRepository = new PlayerRepository(
            resolvedDatabase,
            NullLogger<PlayerRepository>.Instance);
        var playerService = new PlayerService(
            comlinkService,
            playerRepository,
            NullLogger<PlayerService>.Instance);
        var advisorService = new ModAdvisorService(
            NullLogger<ModAdvisorService>.Instance,
            new ModMechanicsService());
        var assignmentService = new ModAssignmentService(
            resolvedDatabase,
            NullLogger<ModAssignmentService>.Instance);
        var scraperService = new SwgohGgScraperService(
            new PerCallHttpClientFactory(),
            resolvedDatabase,
            NullLogger<SwgohGgScraperService>.Instance);

        return new ApplicationComposition(
            resolvedDatabase,
            settings,
            comlinkClient,
            comlinkService,
            playerService,
            advisorService,
            assignmentService,
            scraperService,
            database == null);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ComlinkClient.Dispose();
        if (_ownsDatabase)
        {
            Database.Dispose();
        }
    }

    private static string GetSafeComlinkUrl(string configuredUrl)
    {
        if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out var parsed) &&
            (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
        {
            return parsed.ToString().TrimEnd('/') + "/";
        }

        return "http://localhost:3000/";
    }

    private sealed class PerCallHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
