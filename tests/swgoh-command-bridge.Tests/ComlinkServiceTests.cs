#nullable enable

using System;
using System.Net;
using System.Net.Http;
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
