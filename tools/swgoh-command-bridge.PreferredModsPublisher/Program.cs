#nullable enable

using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;

const int KyberLeague = 100;
const int GacTopLeaderboardType = 6;
const string DefaultDatasetUrl =
    "https://raw.githubusercontent.com/vincentwetzel/swgoh-command-bridge/main/data/preferred-mods/dataset.json";

var comlinkBaseUrl = Environment.GetEnvironmentVariable("COMLINK_BASE_URL") ?? "http://localhost:3000";
if (!Uri.TryCreate(comlinkBaseUrl, UriKind.Absolute, out var comlinkUri) ||
    (comlinkUri.Scheme != Uri.UriSchemeHttp && comlinkUri.Scheme != Uri.UriSchemeHttps))
{
    Console.Error.WriteLine("COMLINK_BASE_URL must be an absolute HTTP(S) URL.");
    return 2;
}

var outputDirectory = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "preferred-mods"));
var maximumAccounts = ReadPositiveInt("PREFERRED_MODS_MAX_ACCOUNTS", 250);
var minimumProfiles = ReadPositiveInt("PREFERRED_MODS_MIN_PROFILES", 100);
var concurrency = ReadPositiveInt("PREFERRED_MODS_CONCURRENCY", 5);
var divisions = ReadDivisions();
var datasetUrl = Environment.GetEnvironmentVariable("PREFERRED_MODS_DATASET_URL") ?? DefaultDatasetUrl;
if (!Uri.TryCreate(datasetUrl, UriKind.Absolute, out var parsedDatasetUrl) || parsedDatasetUrl.Scheme != Uri.UriSchemeHttps)
{
    Console.Error.WriteLine("PREFERRED_MODS_DATASET_URL must be an absolute HTTPS URL.");
    return 2;
}

using var client = new HttpClient
{
    BaseAddress = new Uri(comlinkUri.ToString().TrimEnd('/') + "/", UriKind.Absolute),
    Timeout = TimeSpan.FromSeconds(30)
};
client.DefaultRequestHeaders.UserAgent.ParseAdd("SWGOHCommandBridgePreferredModsPublisher/1.0");

var playerIds = new List<string>();
foreach (var division in divisions)
{
    var leaderboard = await PostJsonAsync(client, "/getLeaderboard", new
    {
        payload = new
        {
            leaderboardType = GacTopLeaderboardType,
            league = KyberLeague,
            division
        },
        enums = false
    });
    playerIds.AddRange(FindPlayerIdsFromJson(leaderboard));
}

var selectedPlayerIds = playerIds
    .Distinct(StringComparer.Ordinal)
    .Take(maximumAccounts)
    .ToList();
if (selectedPlayerIds.Count == 0)
{
    Console.Error.WriteLine("No player IDs were returned by the Kyber GAC leaderboards.");
    return 1;
}

Console.WriteLine($"Fetching {selectedPlayerIds.Count} top Kyber GAC profiles.");
var profiles = new ConcurrentBag<PlayerProfile>();
var failures = new ConcurrentBag<string>();
var parser = new PlayerProfileParser();
await Parallel.ForEachAsync(
    selectedPlayerIds,
    new ParallelOptions { MaxDegreeOfParallelism = concurrency },
    async (playerId, cancellationToken) =>
    {
        try
        {
            var rawProfile = await PostJsonAsync(client, "/player", new
            {
                payload = new { playerId },
                enums = false
            }, cancellationToken);
            profiles.Add(parser.Parse(playerId, rawProfile));
        }
        catch (Exception ex)
        {
            failures.Add($"{playerId}: {ex.Message}");
        }
    });

if (profiles.Count < minimumProfiles)
{
    Console.Error.WriteLine(
        $"Only {profiles.Count} profiles succeeded; {minimumProfiles} are required before publishing. " +
        $"Failures: {string.Join(" | ", failures.Take(5))}");
    return 1;
}

var generatedAtUtc = DateTimeOffset.UtcNow;
var source = new PreferredModsSource(
    "GAC",
    divisions.Select(division => $"Kyber Division {DivisionLabel(division)}").ToList(),
    profiles.Count,
    profiles.Sum(profile => profile.Characters.Count));
var dataset = new PreferredModsAggregator().Aggregate(
    profiles.Select(profile => new PreferredModsObservation(profile)),
    generatedAtUtc.ToString("yyyy-MM-dd.HHmmss", System.Globalization.CultureInfo.InvariantCulture),
    source,
    generatedAtUtc: generatedAtUtc);
PreferredModsDatasetService.ValidateDataset(dataset, "generated preferred-mod dataset");

Directory.CreateDirectory(outputDirectory);
var serializerOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};
var datasetPath = Path.Combine(outputDirectory, "dataset.json");
var datasetBytes = JsonSerializer.SerializeToUtf8Bytes(dataset, serializerOptions);
await File.WriteAllBytesAsync(datasetPath, datasetBytes);
var manifest = new PreferredModsManifest(
    PreferredModsDatasetService.SupportedSchemaVersion,
    dataset.DatasetVersion,
    dataset.GeneratedAtUtc,
    parsedDatasetUrl,
    Convert.ToHexString(SHA256.HashData(datasetBytes)));
await File.WriteAllBytesAsync(
    Path.Combine(outputDirectory, "manifest.json"),
    JsonSerializer.SerializeToUtf8Bytes(manifest, serializerOptions));

Console.WriteLine(
    $"Published aggregate dataset {dataset.DatasetVersion}: {dataset.Characters.Count} characters from {profiles.Count} accounts. " +
    $"Raw player payloads were not written to disk.");
if (!failures.IsEmpty)
{
    Console.WriteLine($"Skipped {failures.Count} profile(s).");
}

return 0;

static async Task<string> PostJsonAsync(
    HttpClient client,
    string path,
    object payload,
    CancellationToken cancellationToken = default)
{
    using var response = await client.PostAsJsonAsync(path, payload, cancellationToken);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadAsStringAsync(cancellationToken);
}

static IEnumerable<string> FindPlayerIdsFromJson(string rawJson)
{
    using var document = JsonDocument.Parse(rawJson);
    return FindPlayerIds(document.RootElement).Distinct(StringComparer.Ordinal).ToList();
}

static IEnumerable<string> FindPlayerIds(JsonElement element)
{
    if (element.ValueKind == JsonValueKind.Object)
    {
        if (element.TryGetProperty("player", out var player))
        {
            if (player.ValueKind == JsonValueKind.Object &&
                player.TryGetProperty("id", out var playerId) &&
                playerId.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(playerId.GetString()))
            {
                yield return playerId.GetString()!;
            }

            if (player.ValueKind == JsonValueKind.Array)
            {
                foreach (var playerEntry in player.EnumerateArray())
                {
                    if (playerEntry.ValueKind == JsonValueKind.Object &&
                        playerEntry.TryGetProperty("id", out var playerEntryId) &&
                        playerEntryId.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(playerEntryId.GetString()))
                    {
                        yield return playerEntryId.GetString()!;
                    }
                }
            }
        }

        foreach (var property in element.EnumerateObject())
        {
            foreach (var nested in FindPlayerIds(property.Value))
            {
                yield return nested;
            }
        }
    }
    else if (element.ValueKind == JsonValueKind.Array)
    {
        foreach (var child in element.EnumerateArray())
        {
            foreach (var nested in FindPlayerIds(child))
            {
                yield return nested;
            }
        }
    }
}

static IReadOnlyList<int> ReadDivisions()
{
    var configured = Environment.GetEnvironmentVariable("PREFERRED_MODS_GAC_DIVISIONS");
    var values = (configured ?? "25,20,15,10,5")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(value => int.TryParse(value, out var parsed) ? parsed : 0)
        .Where(value => value is 5 or 10 or 15 or 20 or 25)
        .Distinct()
        .OrderByDescending(value => value)
        .ToList();
    return values.Count > 0 ? values : new[] { 25, 20, 15, 10, 5 };
}

static int ReadPositiveInt(string name, int fallback) =>
    int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0
        ? value
        : fallback;

static int DivisionLabel(int division) => 6 - (division / 5);
