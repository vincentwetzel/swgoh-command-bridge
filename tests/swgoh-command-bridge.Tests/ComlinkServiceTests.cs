#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class ComlinkServiceTests
{
    [Fact]
    public async Task FetchPlayerRawAsync_RetriesTransientResponses()
    {
        var attempts = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            attempts++;
            var status = attempts < 3
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.OK;

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("{\"name\":\"Test\"}")
            });
        });
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:3000")
        };
        var service = new ComlinkService(client, NullLogger<ComlinkService>.Instance);

        var result = await service.FetchPlayerRawAsync("123456789");

        Assert.Equal(3, attempts);
        Assert.Contains("Test", result);
    }

    [Fact]
    public async Task FetchPlayerRawAsync_DoesNotRetryPermanentClientErrors()
    {
        var attempts = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        });
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:3000")
        };
        var service = new ComlinkService(client, NullLogger<ComlinkService>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.FetchPlayerRawAsync("123456789"));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task FetchMetaDataRawAsync_SendsJsonAcceptAndClientHeaders()
    {
        HttpRequestMessage? observedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            observedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
        });
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:3000")
        };
        var service = new ComlinkService(client, NullLogger<ComlinkService>.Instance);

        await service.FetchMetaDataRawAsync();

        Assert.NotNull(observedRequest);
        Assert.Contains(observedRequest!.Headers.Accept, header =>
            string.Equals(header.MediaType, "application/json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("SWGOHCommandBridge/1.0", observedRequest.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task FetchPlayerRawAsync_SendsTheSupportedJsonRequestContract()
    {
        string? path = null;
        string? body = null;
        string? mediaType = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            path = request.RequestUri?.AbsolutePath;
            body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            mediaType = request.Content?.Headers.ContentType?.MediaType;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
        });
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:3000")
        };
        var service = new ComlinkService(client, NullLogger<ComlinkService>.Instance);

        await service.FetchPlayerRawAsync("123456789");

        Assert.Equal("/player", path);
        Assert.Contains("123456789", body);
        Assert.Equal("application/json", mediaType);
    }

    [Fact]
    public async Task FetchCharacterCatalogAsync_UsesNumericAggregateRequestSegment()
    {
        string? dataBody = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath;
            if (path == "/data")
            {
                dataBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            }

            var response = path switch
            {
                "/metadata" => "{\"latestGamedataVersion\":\"1\",\"latestLocalizationBundleVersion\":\"2\"}",
                "/data" => "{\"units\":[]}",
                "/localization" => "{}",
                _ => throw new InvalidOperationException($"Unexpected path: {path}")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response)
            });
        });
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:3000")
        };
        var service = new ComlinkService(client, NullLogger<ComlinkService>.Instance);

        await service.FetchCharacterCatalogAsync();

        Assert.NotNull(dataBody);
        using var document = JsonDocument.Parse(dataBody!);
        Assert.Equal(0, document.RootElement.GetProperty("payload").GetProperty("requestSegment").GetInt32());
        Assert.False(document.RootElement.GetProperty("payload").TryGetProperty("items", out _));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => _handler(request);
    }
}
