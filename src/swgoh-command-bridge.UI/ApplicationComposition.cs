#nullable enable

using System;
using System.Net.Http;
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
        IPlayerRepository playerRepository,
        DiagnosticEventLog eventLog,
        IModAdvisorService advisorService,
        IModAssignmentService assignmentService,
        ISwgohGgScraperService scraperService,
        bool ownsDatabase)
    {
        Database = database;
        Settings = settings;
        EventLog = eventLog;
        ComlinkClient = comlinkClient;
        ComlinkService = comlinkService;
        PlayerService = playerService;
        PlayerRepository = playerRepository;
        AdvisorService = advisorService;
        AssignmentService = assignmentService;
        ScraperService = scraperService;
        _ownsDatabase = ownsDatabase;
    }

    public AppDbContext Database { get; }

    public ISettingsService Settings { get; }

    public DiagnosticEventLog EventLog { get; }

    public HttpClient ComlinkClient { get; }

    public IComlinkService ComlinkService { get; }

    public IPlayerService PlayerService { get; }

    public IPlayerRepository PlayerRepository { get; }

    public IModAdvisorService AdvisorService { get; }

    public IModAssignmentService AssignmentService { get; }

    public ISwgohGgScraperService ScraperService { get; }

    /// <summary>
    /// Creates the default desktop application service graph.
    /// </summary>
    public static ApplicationComposition CreateDefault(
        AppDbContext? database = null,
        ISettingsService? settingsService = null,
        IPlayerService? playerServiceOverride = null)
    {
        var resolvedDatabase = database ?? new AppDbContext();
        var eventLog = new DiagnosticEventLog();
        var settings = settingsService ?? new SettingsService(new DiagnosticLogger<SettingsService>(eventLog));
        if (settingsService == null)
        {
            settings.LoadSettingsAsync().GetAwaiter().GetResult();
        }

        var comlinkClient = new HttpClient
        {
            BaseAddress = new Uri(GetSafeComlinkUrl(settings.CurrentSettings.ComlinkBaseUrl), UriKind.Absolute)
        };
        var comlinkService = new ComlinkService(
            comlinkClient,
            new DiagnosticLogger<ComlinkService>(eventLog));
        var playerRepository = new PlayerRepository(
            resolvedDatabase,
            new DiagnosticLogger<PlayerRepository>(eventLog));
        var syncHistoryRepository = new SyncHistoryRepository(resolvedDatabase);
        var playerService = playerServiceOverride ?? new PlayerService(
            comlinkService,
            playerRepository,
            new DiagnosticLogger<PlayerService>(eventLog),
            syncHistoryRepository);
        var advisorService = new ModAdvisorService(
            new DiagnosticLogger<ModAdvisorService>(eventLog),
            new ModMechanicsService());
        var assignmentService = new ModAssignmentService(
            resolvedDatabase,
            new DiagnosticLogger<ModAssignmentService>(eventLog));
        var scraperService = new SwgohGgScraperService(
            new PerCallHttpClientFactory(),
            resolvedDatabase,
            new DiagnosticLogger<SwgohGgScraperService>(eventLog),
            () => settings.CurrentSettings.RecommendationContactEmail);

        return new ApplicationComposition(
            resolvedDatabase,
            settings,
            comlinkClient,
            comlinkService,
            playerService,
            playerRepository,
            eventLog,
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
