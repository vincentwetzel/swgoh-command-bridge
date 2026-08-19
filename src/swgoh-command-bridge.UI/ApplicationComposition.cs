#nullable enable

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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
        IComlinkRuntimeManager comlinkRuntimeManager,
        IComlinkService comlinkService,
        ICharacterCatalogSnapshotService characterCatalogService,
        ComlinkCatalogRefreshService catalogRefreshService,
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
        ComlinkRuntimeManager = comlinkRuntimeManager;
        ComlinkService = comlinkService;
        CharacterCatalogService = characterCatalogService;
        CatalogRefreshService = catalogRefreshService;
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

    public IComlinkRuntimeManager ComlinkRuntimeManager { get; }

    public IComlinkService ComlinkService { get; }

    public ICharacterCatalogSnapshotService CharacterCatalogService { get; }

    public ComlinkCatalogRefreshService CatalogRefreshService { get; }

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
        IPlayerService? playerServiceOverride = null,
        IComlinkRuntimeManager? comlinkRuntimeManagerOverride = null)
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
        var comlinkRuntimeManager = comlinkRuntimeManagerOverride ??
            (database == null
                ? new ComlinkRuntimeManager()
                : NullComlinkRuntimeManager.Instance);
        var comlinkService = new ComlinkService(
            comlinkClient,
            new DiagnosticLogger<ComlinkService>(eventLog));
        var characterCatalogService = new BundledCharacterCatalogService();
        var catalogRefreshService = new ComlinkCatalogRefreshService(
            comlinkService,
            characterCatalogService);
        var playerRepository = new PlayerRepository(
            resolvedDatabase,
            new DiagnosticLogger<PlayerRepository>(eventLog));
        var syncHistoryRepository = new SyncHistoryRepository(resolvedDatabase);
        var playerService = playerServiceOverride ?? new PlayerService(
            comlinkService,
            playerRepository,
            new DiagnosticLogger<PlayerService>(eventLog),
            syncHistoryRepository,
            characterCatalogService);
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
            comlinkRuntimeManager,
            comlinkService,
            characterCatalogService,
            catalogRefreshService,
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
        ComlinkRuntimeManager.Dispose();
        ComlinkClient.Dispose();
        if (_ownsDatabase)
        {
            Database.Dispose();
        }
    }

    public async Task<ComlinkRuntimeResult> EnsureComlinkReadyAsync(
        IProgress<ComlinkRuntimeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var requestedAddress = ComlinkClient.BaseAddress ?? new Uri("http://localhost:3000/");
        var result = await ComlinkRuntimeManager.EnsureReadyAsync(
            requestedAddress,
            progress,
            cancellationToken).ConfigureAwait(false);
        ComlinkClient.BaseAddress = result.BaseAddress;
        return result;
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
