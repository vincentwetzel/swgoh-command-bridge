#nullable enable

using System.Threading;
using System.Threading.Tasks;
using swgoh_command_bridge.Core.Database.Entities;

namespace swgoh_command_bridge.Core.Services
{
    /// <summary>
    /// Service for scraping and caching best mod configurations from swgoh.gg.
    /// </summary>
    public interface ISwgohGgScraperService
    {
        /// <summary>
        /// Scrapes swgoh.gg for a single character's best mods recommendation and updates the local cache.
        /// </summary>
        Task<SwgohGgRecommendationEntity?> ScrapeCharacterModsAsync(string characterId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs an incremental batch scrape across a list of character IDs, respecting rate limits.
        /// </summary>
        Task ScrapeBatchIncrementalAsync(string[] characterIds, CancellationToken cancellationToken = default);
    }
}