#nullable enable

using System;
using System.Collections.Generic;
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
    public class ComlinkService : IComlinkService, ICharacterCatalogService
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

        /// <inheritdoc />
        public async Task<CharacterCatalogPayload> FetchCharacterCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            var metadataJson = await FetchMetaDataRawAsync(cancellationToken).ConfigureAwait(false);
            var versions = ReadCatalogVersions(metadataJson);

            // Comlink v4 expects a numeric requestSegment (0 through 4), not
            // the obsolete string 'items' field. Segment 0 is the aggregate
            // game-data response, which keeps new units data-driven.
            var dataPayload = new DataRequestEnvelope(
                new DataRequestPayload(
                    versions.GameDataVersion,
                    IncludePveUnits: false,
                    RequestSegment: 0),
                Enums: false);
            var serializedDataPayload = JsonSerializer.Serialize(
                dataPayload,
                ComlinkSourceGenerationContext.Default.DataRequestEnvelope);
            var gameDataJson = await PostForStringAsync(
                "/data",
                serializedDataPayload,
                cancellationToken).ConfigureAwait(false);

            var localizationPayload = new LocalizationRequestEnvelope(
                new LocalizationRequestPayload($"{versions.LocalizationVersion}:ENG_US"),
                Unzip: true,
                Enums: false);
            var serializedLocalizationPayload = JsonSerializer.Serialize(
                localizationPayload,
                ComlinkSourceGenerationContext.Default.LocalizationRequestEnvelope);
            var localizationJson = await PostForStringAsync(
                "/localization",
                serializedLocalizationPayload,
                cancellationToken).ConfigureAwait(false);

            return new CharacterCatalogPayload(gameDataJson, localizationJson, "Comlink");
        }

        private static (string GameDataVersion, string LocalizationVersion) ReadCatalogVersions(string metadataJson)
        {
            using var document = JsonDocument.Parse(metadataJson);
            var gameDataVersion = FindString(document.RootElement, "latestGamedataVersion");
            var localizationVersion = FindString(document.RootElement, "latestLocalizationBundleVersion");
            if (string.IsNullOrWhiteSpace(gameDataVersion) || string.IsNullOrWhiteSpace(localizationVersion))
            {
                throw new InvalidOperationException(
                    "Comlink metadata did not include the latest game-data and localization versions.");
            }

            return (gameDataVersion, localizationVersion);
        }

        private static string? FindString(JsonElement element, string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                        property.Value.ValueKind == JsonValueKind.String)
                    {
                        return property.Value.GetString();
                    }

                    var nested = FindString(property.Value, propertyName);
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        return nested;
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    var nested = FindString(item, propertyName);
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        return nested;
                    }
                }
            }

            return null;
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

                    if (!response.IsSuccessStatusCode)
                    {
                        var detail = await response.Content
                            .ReadAsStringAsync(cancellationToken)
                            .ConfigureAwait(false);
                        throw new HttpRequestException(
                            $"Comlink request {path} failed with {(int)response.StatusCode}: " +
                            (detail.Length > 1000 ? detail[..1000] : detail),
                            null,
                            response.StatusCode);
                    }

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

    internal record DataRequestPayload(
        string Version,
        bool IncludePveUnits,
        int RequestSegment);

    internal record DataRequestEnvelope(DataRequestPayload Payload, bool Enums);

    internal record LocalizationRequestPayload(string Id);

    internal record LocalizationRequestEnvelope(
        LocalizationRequestPayload Payload,
        bool Unzip,
        bool Enums);

    [JsonSerializable(typeof(PlayerRequestPayload))]
    [JsonSerializable(typeof(PlayerRequestEnvelope))]
    [JsonSerializable(typeof(MetadataRequestEnvelope))]
    [JsonSerializable(typeof(DataRequestEnvelope))]
    [JsonSerializable(typeof(LocalizationRequestEnvelope))]
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    internal partial class ComlinkSourceGenerationContext : JsonSerializerContext
    {
    }
}
