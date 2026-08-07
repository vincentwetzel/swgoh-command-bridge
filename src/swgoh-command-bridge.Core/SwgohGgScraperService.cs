#nullable enable

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services
{
    /// <summary>
    /// Service responsible for scraping best mod sets and primary statistics from swgoh.gg.
    /// </summary>
    public class SwgohGgScraperService : ISwgohGgScraperService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppDbContext _context;
        private readonly ILogger<SwgohGgScraperService> _logger;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private static readonly SwgohGgRecommendationParser RecommendationParser = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="SwgohGgScraperService"/> class.
        /// </summary>
        public SwgohGgScraperService(
            IHttpClientFactory httpClientFactory,
            AppDbContext context,
            ILogger<SwgohGgScraperService> logger)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);

            _httpClientFactory = httpClientFactory;
            _context = context;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<bool> ScrapeCharacterRecommendationsAsync(string characterId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(characterId);

            // Check if recommendation exists and is fresh (less than 7 days old) to protect swgoh.gg from excessive traffic (Rule 12 & 14)
            var existingRec = await _context.SwgohGgRecommendations
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.CharacterId == characterId, cancellationToken)
                .ConfigureAwait(false);

            if (existingRec != null && (DateTime.UtcNow - existingRec.LastUpdatedUtc).TotalDays < 7.0)
            {
                _logger.LogInformation("Recommendation for character {CharacterId} was updated recently ({LastUpdatedUtc} UTC). Skipping scrape.", characterId, existingRec.LastUpdatedUtc);
                return true;
            }

            // swgoh.gg character paths use lowercased slug variants
            var slug = characterId.ToLowerInvariant().Replace("_", "-", StringComparison.Ordinal);
            var requestUri = $"https://swgoh.gg/characters/{slug}/best-mods/";

            _logger.LogInformation("Scraping swgoh.gg recommendations for character {CharacterId} at {Uri}", characterId, requestUri);

            var htmlContent = await FetchHtmlWithRetryAsync(requestUri, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                _logger.LogWarning("Empty response or failed parsing target for {CharacterId}", characterId);
                return false;
            }

            try
            {
                var parsedRecommendations = RecommendationParser.Parse(htmlContent);
                var recommendedSets = parsedRecommendations.Sets;
                var primaryStats = parsedRecommendations.PrimaryStats;

                if (recommendedSets.Count == 0 && primaryStats.Count == 0)
                {
                    _logger.LogWarning("No specific recommendations found in HTML structure for {CharacterId}", characterId);
                    return false;
                }

                var entity = await _context.SwgohGgRecommendations
                    .FirstOrDefaultAsync(r => r.CharacterId == characterId, cancellationToken)
                    .ConfigureAwait(false);

                var isNew = false;
                if (entity == null)
                {
                    isNew = true;
                    entity = new SwgohGgRecommendationEntity { CharacterId = characterId };
                }

                var scrapedAtUtc = DateTime.UtcNow;
                entity.Source = "swgoh.gg";
                entity.RecommendationSchemaVersion = 1;
                entity.SourceUrl = requestUri;
                entity.SetRecommendationsJson = JsonSerializer.Serialize(recommendedSets, SerializerOptions);
                entity.PrimaryStatsJson = JsonSerializer.Serialize(primaryStats, SerializerOptions);
                entity.PopularityPercentage = recommendedSets.Count > 0
                    ? recommendedSets.Max(set => set.Percentage)
                    : primaryStats.Values
                        .SelectMany(values => values)
                        .Select(primary => primary.Percentage)
                        .DefaultIfEmpty(0)
                        .Max();
                entity.LastUpdatedUtc = scrapedAtUtc;

                if (isNew)
                {
                    _context.SwgohGgRecommendations.Add(entity);
                }
                else
                {
                    _context.SwgohGgRecommendations.Update(entity);
                }

                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Successfully saved recommendations to DB cache for {CharacterId}", characterId);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed parsing or persisting scraped mod data for {CharacterId}", characterId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task ScrapeAllCharactersIncrementalAsync(
            IProgress<ScrapeProgress>? progress = null,
            CancellationToken cancellationToken = default,
            string? allyCode = null)
        {
            var normalizedAllyCode = allyCode?.Trim();
            _logger.LogInformation(
                "Starting sequential incremental scrape of cached roster characters for ally code {AllyCode}",
                string.IsNullOrWhiteSpace(normalizedAllyCode) ? "all cached accounts" : normalizedAllyCode);

            var characterQuery = _context.Characters.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(normalizedAllyCode))
            {
                characterQuery = characterQuery.Where(character => character.PlayerAllyCode == normalizedAllyCode);
            }

            var characters = await characterQuery
                .OrderByDescending(character => character.Priority)
                .ThenBy(character => character.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (characters.Count == 0)
            {
                _logger.LogWarning("No characters in DB cache to scrape recommendations for");
                return;
            }

            var total = characters.Count;
            for (int i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var character = characters[i];
                string? errorMessage = null;
                bool success = false;

                try
                {
                    success = await ScrapeCharacterRecommendationsAsync(character.Id, cancellationToken).ConfigureAwait(false);
                    if (!success)
                    {
                        errorMessage = "Scrape returned no data.";
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    _logger.LogError(ex, "Error scraping character {CharacterId}", character.Id);
                }

                if (progress != null)
                {
                    progress.Report(new ScrapeProgress(
                        i + 1,
                        total,
                        character.Id,
                        character.Name,
                        success,
                        errorMessage
                    ));
                }

                // Polite delay of 3 seconds between scraping requests to prevent IP throttling (Rule 11)
                _logger.LogDebug("Waiting 3000ms before processing the next request...");
                if (i < total - 1)
                {
                    await Task.Delay(3000, cancellationToken).ConfigureAwait(false);
                }
            }

            _logger.LogInformation("Incremental scraping run finished. Processed {Total} units.", total);
        }

        /// <inheritdoc />
        public async Task<bool> HasRecommendationAsync(string characterId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(characterId);

            var rec = await _context.SwgohGgRecommendations
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.CharacterId == characterId, cancellationToken)
                .ConfigureAwait(false);

            // Valid and fresh if less than 7 days old
            return rec != null && (DateTime.UtcNow - rec.LastUpdatedUtc).TotalDays < 7.0;
        }

        private async Task<string?> FetchHtmlWithRetryAsync(string requestUri, CancellationToken cancellationToken)
        {
            using var client = _httpClientFactory.CreateClient("SwgohGgClient");

            var retryDelayMs = 2000;
            var maxRetries = 3;

            for (var retry = 0; retry <= maxRetries; retry++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                    request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) SWGOHCommandBridge/1.0");
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));

                    using var response = await client.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken).ConfigureAwait(false);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        if (retry == maxRetries)
                        {
                            _logger.LogWarning("HTTP 429 rate limit persisted for {Uri} after {Attempts} attempts", requestUri, retry + 1);
                            return null;
                        }

                        var delay = GetRetryDelay(response, retryDelayMs);
                        _logger.LogWarning(
                            "HTTP 429 rate limited by swgoh.gg. Retrying {Attempt}/{MaxAttempts} after {DelayMs}ms",
                            retry + 1,
                            maxRetries,
                            delay);
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                        retryDelayMs *= 2; // Exponential backoff
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException ex) when (retry < maxRetries)
                {
                    _logger.LogWarning("Transient request failure: {Message}. Retry {Attempt}/{Max}", ex.Message, retry + 1, maxRetries);
                    await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                    retryDelayMs *= 2;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Terminal exception while connecting to swgoh.gg target {Uri}", requestUri);
                    return null;
                }
            }

            _logger.LogError("Exhausted all network retries without response success for {Uri}", requestUri);
            return null;
        }

        private static int GetRetryDelay(HttpResponseMessage response, int fallbackDelayMs)
        {
            var retryAfter = response.Headers.RetryAfter;
            if (retryAfter?.Delta is { } delta)
            {
                return (int)Math.Clamp(delta.TotalMilliseconds, fallbackDelayMs, 60_000);
            }

            if (retryAfter?.Date is { } retryAt)
            {
                return (int)Math.Clamp(
                    (retryAt - DateTimeOffset.UtcNow).TotalMilliseconds,
                    fallbackDelayMs,
                    60_000);
            }

            return fallbackDelayMs;
        }

    }
}
