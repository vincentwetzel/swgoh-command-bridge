#nullable enable

using System.Threading;
using System.Threading.Tasks;

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
        Task ScrapeAllCharactersIncrementalAsync(CancellationToken cancellationToken = default);
    }
}