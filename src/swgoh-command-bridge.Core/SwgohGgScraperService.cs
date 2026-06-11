#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
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

        // Regex patterns for parsing static HTML blocks on swgoh.gg best mods pages
        private static readonly Regex ModSetRegex = new(@"<div class=""mod-set-image""[^>]*alt=""([^""]+)""[\s\S]*?<div class=""mod-set-percent"">([\d\.]+)%</div>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex PrimaryStatRegex = new(@"Slot (\d)[^>]*>[\s\S]*?<div class=""mod-stat-name"">([^<]+)</div>[\s\S]*?<div class=""mod-stat-percent"">([\d\.]+)%</div>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
                var recommendedSets = ExtractModSets(htmlContent);
                var primaryStats = ExtractPrimaryStats(htmlContent);

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

                entity.SetRecommendationsJson = JsonSerializer.Serialize(recommendedSets, SerializerOptions);
                entity.PrimaryStatsJson = JsonSerializer.Serialize(primaryStats, SerializerOptions);
                entity.PopularityPercentage = 100.0; // Primary default parse set
                entity.LastUpdatedUtc = DateTime.UtcNow;

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed parsing or persisting scraped mod data for {CharacterId}", characterId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task ScrapeAllCharactersIncrementalAsync(IProgress<ScrapeProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting sequential incremental scrape of all cached roster characters");

            var characters = await _context.Characters
                .AsNoTracking()
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

            // Set standard user-agent to look friendly to servers
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) SWGOHCommandBridge/1.0");

            var retryDelayMs = 2000;
            var maxRetries = 3;

            for (var retry = 0; retry <= maxRetries; retry++)
            {
                try
                {
                    using var response = await client.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning("HTTP 429 Rate limited by swgoh.gg. Backing off...");
                        await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
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
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Terminal exception while connecting to swgoh.gg target {Uri}", requestUri);
                    return null;
                }
            }

            _logger.LogError("Exhausted all network retries without response success for {Uri}", requestUri);
            return null;
        }

        private static List<RecommendedSet> ExtractModSets(string htmlContent)
        {
            var recommendations = new List<RecommendedSet>(6); // Pre-allocated capacity optimization (Rule 18)
            var matches = ModSetRegex.Matches(htmlContent);

            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    var setName = match.Groups[1].Value.Trim();
                    var percentage = 50.0; // Fallback default popularity

                    if (match.Groups.Count > 2 && double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedPct))
                    {
                        percentage = parsedPct;
                    }

                    recommendations.Add(new RecommendedSet(setName, percentage));
                }
            }

            // Broad regex fallback for unstructured formats
            if (recommendations.Count == 0)
            {
                var fallbackMatches = Regex.Matches(htmlContent, @"alt=""([^""]+Set)""[\s\S]{0,150}?([\d\.]+)%", RegexOptions.IgnoreCase);
                foreach (Match m in fallbackMatches)
                {
                    var setName = m.Groups[1].Value.Replace("Set", "", StringComparison.OrdinalIgnoreCase).Trim();
                    if (double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
                    {
                        recommendations.Add(new RecommendedSet(setName, pct));
                    }
                }
            }

            return recommendations;
        }

        private static Dictionary<string, List<RecommendedPrimary>> ExtractPrimaryStats(string htmlContent)
        {
            var stats = new Dictionary<string, List<RecommendedPrimary>>(6, StringComparer.Ordinal);
            var matches = PrimaryStatRegex.Matches(htmlContent);

            foreach (Match match in matches)
            {
                if (match.Groups.Count > 2)
                {
                    var slotId = match.Groups[1].Value;
                    var statName = match.Groups[2].Value.Trim();
                    var percentage = 100.0; // Fallback default

                    if (match.Groups.Count > 3 && double.TryParse(match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedPct))
                    {
                        percentage = parsedPct;
                    }

                    var mappedSlot = slotId switch
                    {
                        "1" => "Square",
                        "2" => "Arrow",
                        "3" => "Diamond",
                        "4" => "Triangle",
                        "5" => "Circle",
                        "6" => "Cross",
                        _ => $"Slot_{slotId}"
                    };

                    if (!stats.TryGetValue(mappedSlot, out var list))
                    {
                        list = new List<RecommendedPrimary>();
                        stats[mappedSlot] = list;
                    }

                    list.Add(new RecommendedPrimary(statName, percentage));
                }
            }
            return stats;
        }
    }
}