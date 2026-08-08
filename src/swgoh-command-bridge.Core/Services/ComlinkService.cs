#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace swgoh_command_bridge.Core.Services
{
    /// <summary>
    /// Implementation of IComlinkService utilizing HttpClient for requests.
    /// </summary>
    public class ComlinkService : IComlinkService
    {
        private const int MaxAttempts = 3;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ComlinkService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComlinkService"/> class.
        /// </summary>
        public ComlinkService(HttpClient httpClient, ILogger<ComlinkService> logger)
        {
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(logger);

            _httpClient = httpClient;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<string> FetchPlayerRawAsync(string allyCode, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(allyCode);

            _logger.LogInformation("Fetching raw player data for ally code {AllyCode}", allyCode);

            var payload = new PlayerRequestEnvelope(
                new PlayerRequestPayload(allyCode),
                Enums: false);
            var serializedPayload = JsonSerializer.Serialize(
                payload,
                ComlinkSourceGenerationContext.Default.PlayerRequestEnvelope);

            try
            {
                return await PostForStringAsync(
                    "/player",
                    serializedPayload,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch player raw data for ally code {AllyCode}", allyCode);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<string> FetchMetaDataRawAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching raw game metadata");

            try
            {
                var payload = new MetadataRequestEnvelope(
                    new MetadataRequestPayload(),
                    Enums: false);
                var serializedPayload = JsonSerializer.Serialize(
                    payload,
                    ComlinkSourceGenerationContext.Default.MetadataRequestEnvelope);

                return await PostForStringAsync(
                    "/metadata",
                    serializedPayload,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch raw metadata");
                throw;
            }
        }

        private async Task<string> PostForStringAsync(
            string path,
            string? serializedPayload,
            CancellationToken cancellationToken)
        {
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                HttpResponseMessage response;
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, path);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    request.Headers.UserAgent.ParseAdd("SWGOHCommandBridge/1.0");
                    if (serializedPayload != null)
                    {
                        request.Content = new StringContent(serializedPayload, Encoding.UTF8, "application/json");
                    }

                    // Authentication, when required by a deployment's reverse proxy, is supplied through
                    // HttpClient default headers by the composition root. It is never serialized into settings.
                    response = await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException) when (attempt < MaxAttempts)
                {
                    _logger.LogWarning(
                        "Transient Comlink request failure for {Path}; retrying attempt {Attempt} of {MaxAttempts}",
                        path,
                        attempt + 1,
                        MaxAttempts);
                    await Task.Delay(GetRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < MaxAttempts)
                {
                    _logger.LogWarning(
                        "Comlink request timed out for {Path}; retrying attempt {Attempt} of {MaxAttempts}",
                        path,
                        attempt + 1,
                        MaxAttempts);
                    await Task.Delay(GetRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                using (response)
                {
                    if (IsTransient(response.StatusCode) && attempt < MaxAttempts)
                    {
                        _logger.LogWarning(
                            "Transient Comlink response {StatusCode} for {Path}; retrying attempt {Attempt} of {MaxAttempts}",
                            (int)response.StatusCode,
                            path,
                            attempt + 1,
                            MaxAttempts);
                        await Task.Delay(GetRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException("Comlink request did not complete.");
        }

        private static bool IsTransient(HttpStatusCode statusCode)
        {
            var status = (int)statusCode;
            return status == 408 || status == 429 || status >= 500;
        }

        private static TimeSpan GetRetryDelay(int attempt) =>
            TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt - 1));
    }

    internal record PlayerRequestPayload(string AllyCode);

    internal record PlayerRequestEnvelope(PlayerRequestPayload Payload, bool Enums);

    internal record MetadataRequestPayload;

    internal record MetadataRequestEnvelope(MetadataRequestPayload Payload, bool Enums);

    [JsonSerializable(typeof(PlayerRequestPayload))]
    [JsonSerializable(typeof(PlayerRequestEnvelope))]
    [JsonSerializable(typeof(MetadataRequestEnvelope))]
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    internal partial class ComlinkSourceGenerationContext : JsonSerializerContext
    {
    }
}
