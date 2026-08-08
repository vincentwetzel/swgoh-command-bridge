#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services;

/// <summary>
/// Downloads and owns the local Comlink process used by the desktop application.
/// An already-running local Comlink or a user-configured remote endpoint is respected.
/// </summary>
public sealed class ComlinkRuntimeManager : IComlinkRuntimeManager
{
    // Recent releases currently contain incompatible pkg/V8 bytecode on Windows. Try older
    // release candidates automatically if one exits before its HTTP endpoint is ready.
    private static readonly ManagedRelease[] ManagedReleases =
    [
        new("4.4.0", "https://api.github.com/repos/swgoh-utils/swgoh-comlink/releases/tags/v4.4.0"),
        new("4.2.0", "https://api.github.com/repos/swgoh-utils/swgoh-comlink/releases/tags/v4.2.0"),
    ];
    private const string ApplicationName = "SWGOHCommandBridge";
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(20);

    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly object _processDiagnosticsGate = new();
    private readonly StringBuilder _processDiagnostics = new();
    private Process? _ownedProcess;
    private Uri? _ownedAddress;
    private bool _disposed;

    public ComlinkRuntimeManager()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("SWGOHCommandBridge", "1.0"));
    }

    public async Task<ComlinkRuntimeResult> EnsureReadyAsync(
        Uri requestedBaseAddress,
        IProgress<ComlinkRuntimeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestedBaseAddress);
        ThrowIfDisposed();

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsLocalHttpEndpoint(requestedBaseAddress))
            {
                if (_ownedProcess != null)
                {
                    await StopOwnedProcessAsync(cancellationToken).ConfigureAwait(false);
                }

                Report(progress, "Checking the configured Comlink service...", null);
                await WaitForHealthyAsync(requestedBaseAddress, cancellationToken).ConfigureAwait(false);
                return new ComlinkRuntimeResult(requestedBaseAddress, false);
            }

            if (_ownedAddress != null && !_ownedAddress.Equals(requestedBaseAddress))
            {
                await StopOwnedProcessAsync(cancellationToken).ConfigureAwait(false);
            }

            if (await IsHealthyAsync(requestedBaseAddress, cancellationToken).ConfigureAwait(false))
            {
                Report(progress, "Account service is ready.", 100);
                return new ComlinkRuntimeResult(requestedBaseAddress, false);
            }

            var port = requestedBaseAddress.Port > 0 && IsPortAvailable(requestedBaseAddress.Port)
                ? requestedBaseAddress.Port
                : GetAvailablePort();
            var localAddress = new UriBuilder(requestedBaseAddress)
            {
                Port = port,
            }.Uri;

            Exception? lastFailure = null;
            foreach (var release in ManagedReleases)
            {
                try
                {
                    var executablePath = await EnsureBinaryAsync(release, progress, cancellationToken)
                        .ConfigureAwait(false);
                    Report(progress, "Starting the account service...", 90);
                    StartProcess(executablePath, port);
                    _ownedAddress = localAddress;
                    await WaitForHealthyAsync(localAddress, cancellationToken).ConfigureAwait(false);
                    Report(progress, "Account service is ready.", 100);
                    return new ComlinkRuntimeResult(localAddress, true);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastFailure = ex;
                    await StopOwnedProcessAsync(CancellationToken.None).ConfigureAwait(false);
                    Report(progress, "Trying another compatible account service version...", 80);
                }
            }

            throw new InvalidOperationException(
                "No compatible Windows Comlink runtime could be started.",
                lastFailure);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopOwnedProcessAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            StopOwnedProcessAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch
        {
            // Shutdown must not prevent the desktop process from closing.
        }

        _disposed = true;
        _httpClient.Dispose();
        _lifecycleGate.Dispose();
    }

    private async Task<string> EnsureBinaryAsync(
        ManagedRelease release,
        IProgress<ComlinkRuntimeProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new PlatformNotSupportedException(
                "Automatic Comlink setup currently supports 64-bit Windows only.");
        }

        var directory = Path.Combine(AppDataPaths.ComlinkDirectory, release.Version);
        var executablePath = Path.Combine(directory, "swgoh-comlink.exe");
        if (File.Exists(executablePath) && new FileInfo(executablePath).Length > 0)
        {
            Report(progress, "Checking the account service...", 80);
            return executablePath;
        }

        Directory.CreateDirectory(directory);
        using var installLock = await AcquireInstallLockAsync(directory, cancellationToken).ConfigureAwait(false);
        if (File.Exists(executablePath) && new FileInfo(executablePath).Length > 0)
        {
            Report(progress, "Checking the account service...", 80);
            return executablePath;
        }

        Report(progress, "Downloading the account service...", 10);

        using var releaseResponse = await _httpClient.GetAsync(release.ApiUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        releaseResponse.EnsureSuccessStatusCode();
        await using var releaseStream = await releaseResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var releaseDocument = await JsonDocument.ParseAsync(releaseStream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var asset = FindWindowsAsset(releaseDocument.RootElement);
        if (asset.DownloadUrl == null)
        {
            throw new InvalidOperationException(
                "The Comlink release did not contain a supported Windows executable.");
        }

        var temporaryPath = executablePath + "." + Guid.NewGuid().ToString("N") + ".download";
        var extractionDirectory = executablePath + "." + Guid.NewGuid().ToString("N") + ".extracting";
        try
        {
            using var assetResponse = await _httpClient.GetAsync(
                asset.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            assetResponse.EnsureSuccessStatusCode();
            var totalBytes = assetResponse.Content.Headers.ContentLength;
            {
                await using var source = await assetResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var destination = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.SequentialScan);

                var buffer = new byte[81920];
                long copied = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    copied += read;
                    if (totalBytes is > 0)
                    {
                        Report(progress, "Downloading the account service...", 10 + copied * 65d / totalBytes.Value);
                    }
                }

                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            ValidateDigest(temporaryPath, asset.Digest);

            if (asset.IsArchive)
            {
                if (Directory.Exists(extractionDirectory))
                {
                    Directory.Delete(extractionDirectory, recursive: true);
                }

                ZipFile.ExtractToDirectory(temporaryPath, extractionDirectory);
                var extractedExecutable = Directory.EnumerateFiles(
                        extractionDirectory,
                        "*.exe",
                        SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (extractedExecutable == null)
                {
                    throw new InvalidDataException(
                        "The downloaded Comlink archive did not contain a Windows executable.");
                }

                File.Copy(extractedExecutable, executablePath, overwrite: true);
            }
            else
            {
                File.Move(temporaryPath, executablePath, true);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            if (Directory.Exists(extractionDirectory))
            {
                Directory.Delete(extractionDirectory, recursive: true);
            }
        }

        return executablePath;
    }

    private static async Task<FileStream> AcquireInstallLockAsync(
        string directory,
        CancellationToken cancellationToken)
    {
        var lockPath = Path.Combine(directory, "install.lock");
        while (true)
        {
            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    1,
                    FileOptions.DeleteOnClose);
            }
            catch (IOException)
            {
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void StartProcess(string executablePath, int port)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.Environment["APP_NAME"] = ApplicationName;
        startInfo.Environment["PORT"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        startInfo.Environment["ENABLE_SENTRY"] = "false";
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add(ApplicationName);
        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The Comlink process could not be started.");
        _ownedProcess = process;
        lock (_processDiagnosticsGate)
        {
            _processDiagnostics.Clear();
        }

        _ = DrainAsync(process.StandardOutput, AppendProcessDiagnostic);
        _ = DrainAsync(process.StandardError, AppendProcessDiagnostic);
    }

    private async Task WaitForHealthyAsync(Uri address, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + StartupTimeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsHealthyAsync(address, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            if (_ownedProcess?.HasExited == true)
            {
                var details = GetProcessDiagnostics();
                throw new InvalidOperationException(
                    $"The Comlink process exited before it became ready (exit code {_ownedProcess.ExitCode})." +
                    (string.IsNullOrWhiteSpace(details) ? string.Empty : $" Details: {details}"));
            }

            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("The account service did not become ready in time.");
    }

    private async Task<bool> IsHealthyAsync(Uri address, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                new Uri(address, "/"),
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task StopOwnedProcessAsync(CancellationToken cancellationToken)
    {
        var process = _ownedProcess;
        _ownedProcess = null;
        _ownedAddress = null;
        if (process == null)
        {
            return;
        }

        using (process)
        {
            if (process.HasExited)
            {
                return;
            }

            try
            {
                process.CloseMainWindow();
                await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(3), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task DrainAsync(StreamReader reader, Action<string> append)
    {
        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                append(line);
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void AppendProcessDiagnostic(string line)
    {
        lock (_processDiagnosticsGate)
        {
            if (_processDiagnostics.Length < 2000)
            {
                _processDiagnostics.AppendLine(line.Length > 500 ? line[..500] : line);
            }
        }
    }

    private string GetProcessDiagnostics()
    {
        lock (_processDiagnosticsGate)
        {
            return _processDiagnostics.ToString().Trim();
        }
    }

    private static (string? DownloadUrl, string? Digest, bool IsArchive) FindWindowsAsset(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets))
        {
            return (null, null, false);
        }

        var candidates = assets.EnumerateArray()
            .Select(asset => new
            {
                Name = asset.TryGetProperty("name", out var name) ? name.GetString() : null,
                Url = asset.TryGetProperty("browser_download_url", out var url) ? url.GetString() : null,
                Digest = asset.TryGetProperty("digest", out var digest) ? digest.GetString() : null,
                ContentType = asset.TryGetProperty("content_type", out var contentType) ? contentType.GetString() : null,
            })
            .Where(asset => asset.Name != null && asset.Url != null && IsPotentialWindowsBinary(asset.Name, asset.ContentType))
            .OrderByDescending(asset => asset.Name!.Contains("windows", StringComparison.OrdinalIgnoreCase) || asset.Name.Contains("win", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(asset => asset.Name!.Contains("x64", StringComparison.OrdinalIgnoreCase) || asset.Name.Contains("amd64", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        return candidates == null
            ? (null, null, false)
            : (candidates.Url, candidates.Digest, candidates.Name!.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPotentialWindowsBinary(string? name, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(name) || string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var lowerName = name.ToLowerInvariant();
        if (lowerName.Contains("linux") || lowerName.Contains("darwin") || lowerName.Contains("macos") || lowerName.Contains("osx"))
        {
            return false;
        }

        return lowerName.EndsWith(".exe") ||
               lowerName.EndsWith(".zip") ||
               (!Path.HasExtension(lowerName) &&
                (lowerName.Contains("comlink") ||
                 lowerName.Contains("windows") ||
                 lowerName.Contains("win-")));
    }

    private static void ValidateDigest(string path, string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest) || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        var expected = digest["sha256:".Length..].Trim();
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The downloaded Comlink executable failed integrity verification.");
        }
    }

    private static bool IsLocalHttpEndpoint(Uri address) =>
        address.Scheme == Uri.UriSchemeHttp &&
        (address.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
         IPAddress.TryParse(address.Host, out var ip) && IPAddress.IsLoopback(ip));

    private bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void Report(IProgress<ComlinkRuntimeProgress>? progress, string message, double? percent) =>
        progress?.Report(new ComlinkRuntimeProgress(message, percent));

    private sealed record ManagedRelease(string Version, string ApiUrl);

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ComlinkRuntimeManager));
        }
    }
}

public sealed class NullComlinkRuntimeManager : IComlinkRuntimeManager
{
    public static NullComlinkRuntimeManager Instance { get; } = new();

    public Task<ComlinkRuntimeResult> EnsureReadyAsync(
        Uri requestedBaseAddress,
        IProgress<ComlinkRuntimeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new ComlinkRuntimeProgress("Account service is ready.", 100));
        return Task.FromResult(new ComlinkRuntimeResult(requestedBaseAddress, false));
    }

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Dispose()
    {
    }
}
