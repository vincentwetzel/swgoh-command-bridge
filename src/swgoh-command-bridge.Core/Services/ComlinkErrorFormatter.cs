#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace swgoh_command_bridge.Core.Services;

/// <summary>
/// Converts Comlink failures into actionable, privacy-safe messages for the UI.
/// </summary>
public static class ComlinkErrorFormatter
{
    public static string Describe(Exception exception, string operation)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        if (exception is OperationCanceledException)
        {
            return $"{operation} was cancelled.";
        }

        if (exception is HttpRequestException httpException)
        {
            if (httpException.StatusCode is not { } statusCode)
            {
                return $"{operation} could not reach Comlink. Confirm that the local service is running and retry.";
            }

            var numericStatusCode = (int)statusCode;
            return statusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    $"{operation} was rejected by Comlink (HTTP {numericStatusCode}). Check the local service configuration.",
                HttpStatusCode.NotFound =>
                    $"{operation} reached Comlink, but the endpoint was not found (HTTP 404). Check the configured Comlink URL and version.",
                HttpStatusCode.TooManyRequests =>
                    $"{operation} was rate-limited by Comlink. Wait briefly and retry.",
                _ when numericStatusCode >= 500 =>
                    $"{operation} reached Comlink, but the service returned HTTP {numericStatusCode}. Check the Comlink logs.",
                _ =>
                    $"{operation} received an unexpected HTTP response ({numericStatusCode}) from Comlink. Check the service logs."
            };
        }

        if (exception is JsonException)
        {
            return $"{operation} received malformed JSON from Comlink. Check the Comlink version and response logs.";
        }

        return $"{operation} failed. Check the Comlink service and retry.";
    }
}
