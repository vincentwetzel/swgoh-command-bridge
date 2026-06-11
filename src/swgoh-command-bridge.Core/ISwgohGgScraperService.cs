#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services
{
    /// <summary>
    /// Defines scraping operations for gathering optimal mod recommendations from swgoh.gg.
    /// </summary>
    public interface ISwgohGgScraperService
    {
        /// <summary>
        /// Scrapes swgoh.gg best mods setup for a single character and persists it to the database cache.
        /// </summary>
        Task<bool> ScrapeCharacterRecommendationsAsync(string characterId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Scrapes recommendations sequentially for all characters with a polite delay to respect rate limit policies.
        /// </summary>
        Task ScrapeAllCharactersIncrementalAsync(IProgress<ScrapeProgress>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks whether a fresh recommendation exists in the database for the given character.
        /// </summary>
        Task<bool> HasRecommendationAsync(string characterId, CancellationToken cancellationToken = default);
    }
}