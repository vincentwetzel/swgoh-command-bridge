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

var comlinkBaseUrl = Environment.GetEnvironmentVariable("COMLINK_BASE_URL");
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

var allyCodes = new List<string>();
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
    allyCodes.AddRange(FindAllyCodes(leaderboard));
}

var selectedAllyCodes = allyCodes
    .Distinct(StringComparer.Ordinal)
    .Take(maximumAccounts)
    .ToList();
if (selectedAllyCodes.Count == 0)
{
    Console.Error.WriteLine("No ally codes were returned by the Kyber GAC leaderboards.");
    return 1;
}

Console.WriteLine($"Fetching {selectedAllyCodes.Count} top Kyber GAC profiles.");
var profiles = new ConcurrentBag<PlayerProfile>();
var failures = new ConcurrentBag<string>();
var parser = new PlayerProfileParser();
await Parallel.ForEachAsync(
    selectedAllyCodes,
    new ParallelOptions { MaxDegreeOfParallelism = concurrency },
    async (allyCode, cancellationToken) =>
    {
        try
        {
            var rawProfile = await PostJsonAsync(client, "/player", new
            {
                payload = new { allyCode },
                enums = false
            }, cancellationToken);
            profiles.Add(parser.Parse(allyCode, rawProfile));
        }
        catch (Exception ex)
        {
            failures.Add($"{allyCode}: {ex.Message}");
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

static IEnumerable<string> FindAllyCodes(string rawJson)
{
    using var document = JsonDocument.Parse(rawJson);
    return FindAllyCodes(document.RootElement).Distinct(StringComparer.Ordinal).ToList();
}

static IEnumerable<string> FindAllyCodes(JsonElement element)
{
    if (element.ValueKind == JsonValueKind.Object)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, "allyCode", StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            {
                var candidate = property.Value.ToString().Replace("-", string.Empty, StringComparison.Ordinal);
                if (candidate.Length == 9 && candidate.All(char.IsAsciiDigit))
                {
                    yield return candidate;
                }
            }

            foreach (var nested in FindAllyCodes(property.Value))
            {
                yield return nested;
            }
        }
    }
    else if (element.ValueKind == JsonValueKind.Array)
    {
        foreach (var child in element.EnumerateArray())
        {
            foreach (var nested in FindAllyCodes(child))
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
