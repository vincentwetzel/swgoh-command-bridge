#nullable enable

using System.Threading;
using System.Threading.Tasks;
using swgoh_command_bridge.Core.Database.Entities;

namespace swgoh_command_bridge.Core.Database.Repositories
{
    /// <summary>
    /// Interface for executing caching actions on player metadata.
    /// </summary>
    public interface IPlayerRepository
    {
        /// <summary>
        /// Fetches the cached player profile with tracked or untracked relations.
        /// </summary>
        Task<PlayerEntity?> GetPlayerAsync(string allyCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Persists or updates the player profile and its deep relations inside the cache.
        /// </summary>
        Task SavePlayerAsync(PlayerEntity player, CancellationToken cancellationToken = default);
    }
}