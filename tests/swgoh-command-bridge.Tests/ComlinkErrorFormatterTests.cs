#nullable enable

using System.Net;
using System.Net.Http;
using System.Text.Json;
using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class ComlinkErrorFormatterTests
{
    [Fact]
    public void Describe_IdentifiesUnavailableServiceWithoutLeakingExceptionText()
    {
        var exception = new HttpRequestException(
            "Sensitive URL or token should not be shown",
            null,
            HttpStatusCode.ServiceUnavailable);

        var message = ComlinkErrorFormatter.Describe(exception, "Account sync");

        Assert.Contains("HTTP 503", message);
        Assert.Contains("Comlink logs", message);
        Assert.DoesNotContain("Sensitive", message);
    }

    [Fact]
    public void Describe_IdentifiesEndpointMismatch()
    {
        var message = ComlinkErrorFormatter.Describe(
            new HttpRequestException("not found", null, HttpStatusCode.NotFound),
            "Connection test");

        Assert.Contains("endpoint was not found", message);
        Assert.Contains("configured Comlink URL", message);
    }

    [Fact]
    public void Describe_IdentifiesMalformedPayload()
    {
        var message = ComlinkErrorFormatter.Describe(
            new JsonException("payload details"),
            "Account sync");

        Assert.Contains("malformed JSON", message);
        Assert.DoesNotContain("payload details", message);
    }
}
