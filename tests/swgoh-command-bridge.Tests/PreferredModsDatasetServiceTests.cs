#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class PreferredModsDatasetServiceTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task RefreshIfDueAsync_ValidManifest_AtomicallyActivatesDownloadedDataset()
    {
        var cacheDirectory = CreateTestDirectory();
        try
        {
            var bundled = Serialize(CreateDataset("bundled"));
            var downloaded = Serialize(CreateDataset("remote-1"));
            var manifest = new PreferredModsManifest(
                1,
                "remote-1",
                DateTimeOffset.Parse("2026-08-19T00:00:00Z"),
                new Uri("https://example.test/dataset.json"),
                Convert.ToHexString(SHA256.HashData(downloaded)));
            using var client = new HttpClient(new StubHandler(new Dictionary<string, byte[]>
            {
                ["https://example.test/manifest.json"] = Serialize(manifest),
                ["https://example.test/dataset.json"] = downloaded
            }));
            var service = new PreferredModsDatasetService(
                client,
                new PreferredModsUpdateOptions(new Uri("https://example.test/manifest.json"), TimeSpan.Zero),
                cacheDirectory,
                () => bundled);
            var changes = 0;
            service.DatasetChanged += (_, _) => changes++;

            var result = await service.RefreshIfDueAsync();

            Assert.Equal(PreferredModsRefreshStatus.Updated, result.Status);
            Assert.Equal("remote-1", service.Current.DatasetVersion);
            Assert.Equal(1, changes);
            Assert.True(File.Exists(Path.Combine(cacheDirectory, "current.json")));
            Assert.True(File.Exists(Path.Combine(cacheDirectory, "state.json")));
        }
        finally
        {
            DeleteTestDirectory(cacheDirectory);
        }
    }

    [Fact]
    public async Task RefreshIfDueAsync_BadChecksum_PreservesBundledDataset()
    {
        var cacheDirectory = CreateTestDirectory();
        try
        {
            var bundled = Serialize(CreateDataset("bundled"));
            var downloaded = Serialize(CreateDataset("remote-1"));
            var manifest = new PreferredModsManifest(
                1,
                "remote-1",
                DateTimeOffset.Parse("2026-08-19T00:00:00Z"),
                new Uri("https://example.test/dataset.json"),
                new string('A', 64));
            using var client = new HttpClient(new StubHandler(new Dictionary<string, byte[]>
            {
                ["https://example.test/manifest.json"] = Serialize(manifest),
                ["https://example.test/dataset.json"] = downloaded
            }));
            var service = new PreferredModsDatasetService(
                client,
                new PreferredModsUpdateOptions(new Uri("https://example.test/manifest.json"), TimeSpan.Zero),
                cacheDirectory,
                () => bundled);

            var result = await service.RefreshIfDueAsync();

            Assert.Equal(PreferredModsRefreshStatus.Failed, result.Status);
            Assert.Equal("bundled", service.Current.DatasetVersion);
            Assert.False(File.Exists(Path.Combine(cacheDirectory, "current.json")));
        }
        finally
        {
            DeleteTestDirectory(cacheDirectory);
        }
    }

    private static PreferredModsDataset CreateDataset(string version) => new(
        1,
        version,
        DateTimeOffset.Parse("2026-08-19T00:00:00Z"),
        new PreferredModsSource("GAC", Array.Empty<string>(), 0, 0),
        Array.Empty<PreferredCharacterRecommendation>());

    private static byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);

    private static string CreateTestDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "swgoh-command-bridge-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTestDirectory(string path)
    {
        if (Directory.Exists(path) && path.Contains("swgoh-command-bridge-tests", StringComparison.OrdinalIgnoreCase))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, byte[]> _responses;

        public StubHandler(IReadOnlyDictionary<string, byte[]> responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri != null && _responses.TryGetValue(request.RequestUri.ToString(), out var body))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(body)
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
