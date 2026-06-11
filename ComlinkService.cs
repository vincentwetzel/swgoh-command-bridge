#nullable enable

using System;
using System.Net.Http;
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

            var payload = new PlayerRequestPayload(allyCode);
            using var content = new StringContent(JsonSerializer.Serialize(payload, ComlinkSourceGenerationContext.Default.PlayerRequestPayload), Encoding.UTF8, "application/json");

            try
            {
                using var response = await _httpClient.PostAsync("/player", content, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return result;
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
                using var response = await _httpClient.PostAsync("/metadata", null, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch raw metadata");
                throw;
            }
        }
    }

    internal record PlayerRequestPayload(string AllyCode);

    [JsonSerializable(typeof(PlayerRequestPayload))]
    internal partial class ComlinkSourceGenerationContext : JsonSerializerContext
    {
    }
}