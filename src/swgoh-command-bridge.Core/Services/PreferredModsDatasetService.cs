#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services;

/// <summary>Loads global preferred-mod data and silently maintains its offline cache.</summary>
public interface IPreferredModsDatasetService
{
    event EventHandler? DatasetChanged;

    PreferredModsDataset Current { get; }

    PreferredModsDatasetInfo GetDatasetInfo();

    Task<PreferredModsRefreshResult> RefreshIfDueAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>Configuration owned by the application publisher, not individual users.</summary>
public sealed record PreferredModsUpdateOptions(
    Uri? ManifestUrl,
    TimeSpan UpdateInterval,
    int MaximumDatasetBytes = 8 * 1024 * 1024)
{
    public static PreferredModsUpdateOptions Default { get; } = new(
        new Uri(
            "https://raw.githubusercontent.com/vincentwetzel/swgoh-command-bridge/main/data/preferred-mods/manifest.json",
            UriKind.Absolute),
        TimeSpan.FromDays(7));
}

/// <summary>
/// File-backed, validated preferred-mod dataset cache. A bundled baseline is
/// always retained so offline operation never depends on GitHub availability.
/// </summary>
public sealed class PreferredModsDatasetService : IPreferredModsDatasetService
{
    public const int SupportedSchemaVersion = 1;

    private const string DatasetFileName = "current.json";
    private const string StateFileName = "state.json";
    private const int StateVersion = 1;
    private const string EmbeddedResourceSuffix = ".Assets.PreferredMods.preferred-mods.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly PreferredModsUpdateOptions _options;
    private readonly string _cacheDirectory;
    private readonly Func<byte[]> _readBundledDataset;
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private PreferredModsDataset _current;

    public PreferredModsDatasetService(
        HttpClient httpClient,
        PreferredModsUpdateOptions? options = null,
        string? cacheDirectory = null,
        Func<byte[]>? readBundledDataset = null,
        TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
        _options = options ?? PreferredModsUpdateOptions.Default;
        _cacheDirectory = cacheDirectory ?? AppDataPaths.PreferredModsDirectory;
        _readBundledDataset = readBundledDataset ?? ReadBundledDataset;
        _clock = clock ?? TimeProvider.System;
        _current = LoadBestAvailableDataset();
    }

    public event EventHandler? DatasetChanged;

    public PreferredModsDataset Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public PreferredModsDatasetInfo GetDatasetInfo()
    {
        var dataset = Current;
        return new PreferredModsDatasetInfo(
            dataset.DatasetVersion,
            dataset.GeneratedAtUtc,
            dataset.Source.AccountCount,
            dataset.Characters.Count,
            dataset.Source.GameMode);
    }

    public async Task<PreferredModsRefreshResult> RefreshIfDueAsync(
        CancellationToken cancellationToken = default)
    {
        if (_options.ManifestUrl is not { IsAbsoluteUri: true } manifestUrl)
        {
            return Result(PreferredModsRefreshStatus.Disabled, "Preferred-mod updates are not configured.");
        }

        if (manifestUrl.Scheme != Uri.UriSchemeHttps)
        {
            return Result(PreferredModsRefreshStatus.Disabled, "Preferred-mod manifest URL must use HTTPS.");
        }

        var state = TryReadState();
        var now = _clock.GetUtcNow();
        if (Current.Source.AccountCount > 0 &&
            state?.LastCheckedAtUtc is DateTimeOffset lastChecked &&
            now - lastChecked < _options.UpdateInterval)
        {
            return Result(PreferredModsRefreshStatus.NotDue, "Preferred-mod data was checked recently.");
        }

        try
        {
            using var manifestResponse = await _httpClient
                .GetAsync(manifestUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            manifestResponse.EnsureSuccessStatusCode();
            var manifestBytes = await ReadBoundedBytesAsync(
                    manifestResponse.Content,
                    256 * 1024,
                    cancellationToken)
                .ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<PreferredModsManifest>(manifestBytes, SerializerOptions)
                ?? throw new InvalidDataException("Preferred-mod manifest was empty.");
            ValidateManifest(manifest);

            if (string.Equals(manifest.DatasetVersion, Current.DatasetVersion, StringComparison.Ordinal))
            {
                WriteState(new PreferredModsCacheState(StateVersion, now));
                return Result(PreferredModsRefreshStatus.Current, "Preferred-mod data is already current.");
            }

            using var datasetResponse = await _httpClient
                .GetAsync(manifest.DatasetUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            datasetResponse.EnsureSuccessStatusCode();
            var datasetBytes = await ReadBoundedBytesAsync(
                    datasetResponse.Content,
                    _options.MaximumDatasetBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            var actualHash = Convert.ToHexString(SHA256.HashData(datasetBytes));
            if (!string.Equals(actualHash, manifest.DatasetSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Preferred-mod dataset checksum did not match its manifest.");
            }

            var dataset = DeserializeAndValidate(datasetBytes, "downloaded preferred-mod dataset");
            if (!string.Equals(dataset.DatasetVersion, manifest.DatasetVersion, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Preferred-mod dataset version did not match its manifest.");
            }

            WriteDatasetAtomically(datasetBytes);
            WriteState(new PreferredModsCacheState(StateVersion, now));
            lock (_gate)
            {
                _current = dataset;
            }

            DatasetChanged?.Invoke(this, EventArgs.Empty);
            return Result(PreferredModsRefreshStatus.Updated, "Preferred-mod data was updated.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidDataException or NotSupportedException)
        {
            TryWriteStateAfterFailure(now);
            return Result(PreferredModsRefreshStatus.Failed, $"Preferred-mod update skipped: {ex.Message}");
        }
    }

    public static PreferredModsDataset DeserializeAndValidate(ReadOnlyMemory<byte> bytes, string source)
    {
        var dataset = JsonSerializer.Deserialize<PreferredModsDataset>(bytes.Span, SerializerOptions)
            ?? throw new InvalidDataException($"The {source} was empty.");
        ValidateDataset(dataset, source);
        return dataset;
    }

    public static void ValidateDataset(PreferredModsDataset dataset, string source)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        if (dataset.SchemaVersion != SupportedSchemaVersion)
        {
            throw new InvalidDataException(
                $"The {source} has unsupported schema version {dataset.SchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(dataset.DatasetVersion) || dataset.Source == null || dataset.Characters == null)
        {
            throw new InvalidDataException($"The {source} is missing required metadata.");
        }

        if (dataset.Source.AccountCount < 0 || dataset.Source.ObservationCount < 0)
        {
            throw new InvalidDataException($"The {source} has invalid source counts.");
        }

        var characterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var character in dataset.Characters)
        {
            if (character == null || string.IsNullOrWhiteSpace(character.CharacterId) ||
                character.SampleSize < 0 || character.Slots == null || character.Setups == null ||
                character.QualityProfiles == null || !characterIds.Add(character.CharacterId))
            {
                throw new InvalidDataException($"The {source} has an invalid or duplicate character recommendation.");
            }

            var slots = new HashSet<ModSlot>();
            foreach (var slot in character.Slots)
            {
                if (slot == null || !Enum.IsDefined(slot.Slot) || !slots.Add(slot.Slot) || slot.Options == null)
                {
                    throw new InvalidDataException($"The {source} has an invalid slot recommendation.");
                }

                var primaries = new HashSet<StatType>();
                foreach (var option in slot.Options)
                {
                    if (option == null || option.PrimaryStat == StatType.None ||
                        !Enum.IsDefined(option.PrimaryStat) || !primaries.Add(option.PrimaryStat) ||
                        option.Share is < 0 or > 1 || option.Observations < 0 ||
                        !Enum.IsDefined(option.Status))
                    {
                        throw new InvalidDataException($"The {source} has an invalid primary-stat option.");
                    }
                }

                if (slot.Options.Sum(option => option.Share) > 1.0001)
                {
                    throw new InvalidDataException($"The {source} has a primary distribution above 100%.");
                }
            }
        }
    }

    private PreferredModsDataset LoadBestAvailableDataset()
    {
        if (TryReadCachedDataset(out var cached))
        {
            return cached;
        }

        return DeserializeAndValidate(_readBundledDataset(), "bundled preferred-mod dataset");
    }

    private bool TryReadCachedDataset(out PreferredModsDataset dataset)
    {
        dataset = default!;
        try
        {
            var path = Path.Combine(_cacheDirectory, DatasetFileName);
            if (!File.Exists(path))
            {
                return false;
            }

            dataset = DeserializeAndValidate(File.ReadAllBytes(path), "cached preferred-mod dataset");
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private PreferredModsCacheState? TryReadState()
    {
        try
        {
            var path = Path.Combine(_cacheDirectory, StateFileName);
            if (!File.Exists(path))
            {
                return null;
            }

            var state = JsonSerializer.Deserialize<PreferredModsCacheState>(
                File.ReadAllBytes(path),
                SerializerOptions);
            return state?.Version == StateVersion ? state : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void WriteDatasetAtomically(byte[] contents)
    {
        Directory.CreateDirectory(_cacheDirectory);
        WriteAtomically(Path.Combine(_cacheDirectory, DatasetFileName), contents);
    }

    private void WriteState(PreferredModsCacheState state)
    {
        Directory.CreateDirectory(_cacheDirectory);
        WriteAtomically(
            Path.Combine(_cacheDirectory, StateFileName),
            JsonSerializer.SerializeToUtf8Bytes(state, SerializerOptions));
    }

    private void TryWriteStateAfterFailure(DateTimeOffset attemptedAtUtc)
    {
        try
        {
            WriteState(new PreferredModsCacheState(StateVersion, attemptedAtUtc));
        }
        catch (IOException)
        {
            // An unavailable cache directory must not turn a silent update into a startup failure.
        }
        catch (UnauthorizedAccessException)
        {
            // An unavailable cache directory must not turn a silent update into a startup failure.
        }
    }

    private static async Task<byte[]> ReadBoundedBytesAsync(
        HttpContent content,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is long length && length > maximumBytes)
        {
            throw new InvalidDataException($"Preferred-mod response exceeds {maximumBytes:N0} bytes.");
        }

        await using var input = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (output.Length + read > maximumBytes)
            {
                throw new InvalidDataException($"Preferred-mod response exceeds {maximumBytes:N0} bytes.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return output.ToArray();
    }

    private static void ValidateManifest(PreferredModsManifest manifest)
    {
        if (manifest.SchemaVersion != SupportedSchemaVersion ||
            string.IsNullOrWhiteSpace(manifest.DatasetVersion) ||
            manifest.DatasetUrl == null || !manifest.DatasetUrl.IsAbsoluteUri ||
            manifest.DatasetUrl.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(manifest.DatasetSha256) || manifest.DatasetSha256.Length != 64 ||
            !manifest.DatasetSha256.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("Preferred-mod manifest is invalid or unsupported.");
        }
    }

    private static void WriteAtomically(string destinationPath, byte[] contents)
    {
        var temporaryPath = destinationPath + ".tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, contents);
            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private PreferredModsRefreshResult Result(PreferredModsRefreshStatus status, string message) =>
        new(status, message, GetDatasetInfo());

    private static byte[] ReadBundledDataset()
    {
        var assembly = typeof(PreferredModsDatasetService).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(EmbeddedResourceSuffix, StringComparison.OrdinalIgnoreCase));
        if (resourceName == null)
        {
            throw new InvalidDataException("Bundled preferred-mod dataset resource was not found.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException("Bundled preferred-mod dataset resource could not be opened.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private sealed record PreferredModsCacheState(int Version, DateTimeOffset LastCheckedAtUtc);
}
