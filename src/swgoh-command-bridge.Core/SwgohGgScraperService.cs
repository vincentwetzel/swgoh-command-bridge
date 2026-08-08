#nullable enable

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
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
        private readonly Func<string?>? _contactEmailProvider;
        private readonly ScrapeRetryPolicy _retryPolicy;
        private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

        private const int MaxRecommendationPageBytes = 2 * 1024 * 1024;

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
            ILogger<SwgohGgScraperService> logger,
            Func<string?>? contactEmailProvider = null,
            ScrapeRetryPolicy? retryPolicy = null,
            Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);

            _httpClientFactory = httpClientFactory;
            _context = context;
            _logger = logger;
            _contactEmailProvider = contactEmailProvider;
            _retryPolicy = retryPolicy ?? new ScrapeRetryPolicy();
            _delayAsync = delayAsync ?? ((delay, cancellationToken) =>
                Task.Delay(delay, cancellationToken));
        }

        /// <inheritdoc />
        public async Task<bool> ScrapeCharacterRecommendationsAsync(
            string characterId,
            CancellationToken cancellationToken = default,
            string? allyCode = null)
        {
            var result = await ScrapeCharacterRecommendationsWithResultAsync(
                characterId,
                cancellationToken,
                allyCode).ConfigureAwait(false);
            return result.Success;
        }

        /// <inheritdoc />
        public async Task<ScrapeCharacterResult> ScrapeCharacterRecommendationsWithResultAsync(
            string characterId,
            CancellationToken cancellationToken = default,
            string? allyCode = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(characterId);
            var normalizedAllyCode = NormalizeOptionalAllyCode(allyCode);

            // Check if recommendation exists and is fresh (less than 7 days old) to protect swgoh.gg from excessive traffic (Rule 12 & 14)
            var existingRec = await _context.SwgohGgRecommendations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    r => r.CharacterId == characterId && r.PlayerAllyCode == normalizedAllyCode,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existingRec != null && (DateTime.UtcNow - existingRec.LastUpdatedUtc).TotalDays < 7.0)
            {
                _logger.LogInformation("Recommendation for character {CharacterId} was updated recently ({LastUpdatedUtc} UTC). Skipping scrape.", characterId, existingRec.LastUpdatedUtc);
                return new ScrapeCharacterResult(true, SkippedFreshData: true);
            }

            // swgoh.gg character paths use lowercased slug variants
            var slug = characterId.ToLowerInvariant().Replace("_", "-", StringComparison.Ordinal);
            var requestUri = $"https://swgoh.gg/characters/{Uri.EscapeDataString(slug)}/best-mods/";

            _logger.LogInformation("Scraping swgoh.gg recommendations for character {CharacterId} at {Uri}", characterId, requestUri);

            var fetchResult = await FetchHtmlWithRetryAsync(requestUri, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(fetchResult.Content))
            {
                var errorMessage = fetchResult.ErrorMessage ?? "No recommendation page content was returned.";
                _logger.LogWarning(
                    "Recommendation refresh failed for {CharacterId}: {ErrorMessage}",
                    characterId,
                    errorMessage);
                return new ScrapeCharacterResult(false, errorMessage);
            }

            try
            {
                var parsedRecommendations = RecommendationParser.Parse(fetchResult.Content);
                var recommendedSets = parsedRecommendations.Sets;
                var primaryStats = parsedRecommendations.PrimaryStats;

                if (recommendedSets.Count == 0 && primaryStats.Count == 0)
                {
                    _logger.LogWarning("No specific recommendations found in HTML structure for {CharacterId}", characterId);
                    return new ScrapeCharacterResult(
                        false,
                        "The recommendation page contained no recognized sets or primary stats.");
                }

                var entity = await _context.SwgohGgRecommendations
                    .FirstOrDefaultAsync(
                        r => r.CharacterId == characterId && r.PlayerAllyCode == normalizedAllyCode,
                        cancellationToken)
                    .ConfigureAwait(false);

                var isNew = false;
                if (entity == null)
                {
                    isNew = true;
                    entity = new SwgohGgRecommendationEntity
                    {
                        CharacterId = characterId,
                        PlayerAllyCode = normalizedAllyCode
                    };
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
                return new ScrapeCharacterResult(true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed parsing or persisting scraped mod data for {CharacterId}", characterId);
                return new ScrapeCharacterResult(
                    false,
                    "The recommendation page could not be parsed or cached. Check the scraper fixture and retry.");
            }
        }

        /// <inheritdoc />
        public async Task ScrapeAllCharactersIncrementalAsync(
            IProgress<ScrapeProgress>? progress = null,
            CancellationToken cancellationToken = default,
            string? allyCode = null)
        {
            var normalizedAllyCode = NormalizeOptionalAllyCode(allyCode);
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
                    var result = await ScrapeCharacterRecommendationsWithResultAsync(
                        character.Id,
                        cancellationToken,
                        string.IsNullOrWhiteSpace(normalizedAllyCode)
                            ? character.PlayerAllyCode
                            : normalizedAllyCode).ConfigureAwait(false);
                    success = result.Success;
                    errorMessage = result.ErrorMessage;
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

                if (i < total - 1)
                {
                    _logger.LogDebug(
                        "Waiting {DelayMs}ms before processing the next request...",
                        _retryPolicy.InterRequestDelay.TotalMilliseconds);
                    await _delayAsync(_retryPolicy.InterRequestDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            _logger.LogInformation("Incremental scraping run finished. Processed {Total} units.", total);
        }

        /// <inheritdoc />
        public async Task<bool> HasRecommendationAsync(
            string characterId,
            CancellationToken cancellationToken = default,
            string? allyCode = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(characterId);
            var normalizedAllyCode = NormalizeOptionalAllyCode(allyCode);

            var rec = await _context.SwgohGgRecommendations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    r => r.CharacterId == characterId && r.PlayerAllyCode == normalizedAllyCode,
                    cancellationToken)
                .ConfigureAwait(false);

            // Valid and fresh if less than 7 days old
            return rec != null && (DateTime.UtcNow - rec.LastUpdatedUtc).TotalDays < 7.0;
        }

        private async Task<ScrapeFetchResult> FetchHtmlWithRetryAsync(
            string requestUri,
            CancellationToken cancellationToken)
        {
            using var client = _httpClientFactory.CreateClient("SwgohGgClient");

            for (var attempt = 1; attempt <= _retryPolicy.MaxAttempts; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                    request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) SWGOHCommandBridge/1.0");
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
                    AddContactHeader(request);

                    using var response = await client.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken).ConfigureAwait(false);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        if (attempt == _retryPolicy.MaxAttempts)
                        {
                            _logger.LogWarning("HTTP 429 rate limit persisted for {Uri} after {Attempts} attempts", requestUri, attempt);
                            return new ScrapeFetchResult(
                                null,
                                $"swgoh.gg rate limiting persisted after {attempt} attempts.");
                        }

                        var delay = GetRetryDelay(response, attempt);
                        _logger.LogWarning(
                            "HTTP 429 rate limited by swgoh.gg. Retrying {Attempt}/{MaxAttempts} after {DelayMs}ms",
                            attempt,
                            _retryPolicy.MaxAttempts,
                            delay.TotalMilliseconds);
                        await _delayAsync(delay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (IsTransient(response.StatusCode) && attempt < _retryPolicy.MaxAttempts)
                    {
                        var delay = GetRetryDelay(response, attempt);
                        _logger.LogWarning(
                            "Transient swgoh.gg response {StatusCode}. Retrying {Attempt}/{MaxAttempts} after {DelayMs}ms",
                            (int)response.StatusCode,
                            attempt,
                            _retryPolicy.MaxAttempts,
                            delay.TotalMilliseconds);
                        await _delayAsync(delay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        return new ScrapeFetchResult(
                            null,
                            $"swgoh.gg returned HTTP {(int)response.StatusCode} for the character page.");
                    }

                    var contentLength = response.Content.Headers.ContentLength;
                    if (contentLength > MaxRecommendationPageBytes)
                    {
                        return new ScrapeFetchResult(
                            null,
                            "The recommendation page exceeded the safe response-size limit.");
                    }

                    var content = await ReadBoundedContentAsync(
                        response.Content,
                        cancellationToken).ConfigureAwait(false);
                    if (content == null)
                    {
                        return new ScrapeFetchResult(
                            null,
                            "The recommendation page exceeded the safe response-size limit.");
                    }

                    return new ScrapeFetchResult(
                        content,
                        null);
                }
                catch (HttpRequestException ex) when (attempt < _retryPolicy.MaxAttempts)
                {
                    var delay = _retryPolicy.GetBackoff(attempt);
                    _logger.LogWarning(
                        "Transient request failure: {Message}. Retry {Attempt}/{Max} after {DelayMs}ms",
                        ex.Message,
                        attempt,
                        _retryPolicy.MaxAttempts,
                        delay.TotalMilliseconds);
                    await _delayAsync(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Network retries exhausted while connecting to swgoh.gg target {Uri}", requestUri);
                    return new ScrapeFetchResult(
                        null,
                        $"Could not reach swgoh.gg after {attempt} attempts.");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Terminal exception while connecting to swgoh.gg target {Uri}", requestUri);
                    return new ScrapeFetchResult(null, "The recommendation request failed unexpectedly.");
                }
            }

            _logger.LogError("Exhausted all network retries without response success for {Uri}", requestUri);
            return new ScrapeFetchResult(null, "The recommendation request exhausted its retry policy.");
        }

        private static bool IsTransient(HttpStatusCode statusCode) =>
            statusCode == HttpStatusCode.RequestTimeout ||
            statusCode == HttpStatusCode.TooManyRequests ||
            (int)statusCode >= 500;

        private static string NormalizeOptionalAllyCode(string? allyCode)
        {
            if (string.IsNullOrWhiteSpace(allyCode))
            {
                return string.Empty;
            }

            return AllyCodeValidator.NormalizeOrThrow(allyCode);
        }

        private void AddContactHeader(HttpRequestMessage request)
        {
            var contact = _contactEmailProvider?.Invoke()?.Trim();
            if (string.IsNullOrWhiteSpace(contact))
            {
                return;
            }

            try
            {
                request.Headers.From = contact;
            }
            catch (FormatException)
            {
                _logger.LogWarning("Ignoring invalid configured recommendation contact metadata.");
            }
        }

        private static async Task<string?> ReadBoundedContentAsync(
            HttpContent content,
            CancellationToken cancellationToken)
        {
            await using var responseStream = await content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var bufferStream = new System.IO.MemoryStream();
            var buffer = new byte[81920];
            var totalBytes = 0;

            while (true)
            {
                var bytesRead = await responseStream
                    .ReadAsync(buffer.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                totalBytes += bytesRead;
                if (totalBytes > MaxRecommendationPageBytes)
                {
                    return null;
                }

                await bufferStream.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    cancellationToken).ConfigureAwait(false);
            }

            return System.Text.Encoding.UTF8.GetString(bufferStream.ToArray());
        }

        private sealed record ScrapeFetchResult(string? Content, string? ErrorMessage);

        private TimeSpan GetRetryDelay(HttpResponseMessage response, int retryNumber)
        {
            TimeSpan? serverDelay = null;
            var retryAfter = response.Headers.RetryAfter;
            if (retryAfter?.Delta is { } delta)
            {
                serverDelay = delta;
            }
            else if (retryAfter?.Date is { } retryAt)
            {
                serverDelay = retryAt - DateTimeOffset.UtcNow;
            }

            return _retryPolicy.GetDelay(retryNumber, serverDelay);
        }

    }
}
