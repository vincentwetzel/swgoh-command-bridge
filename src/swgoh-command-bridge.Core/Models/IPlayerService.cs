#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services
{
    /// <summary>
    /// Service interface to fetch and manage player profiles, characters, and mods.
    /// </summary>
    public interface IPlayerService
    {
        /// <summary>
        /// Fetches the player details and updates/returns the models.
        /// </summary>
        Task<PlayerProfile> GetPlayerProfileAsync(string allyCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetches raw player details, maps them to database entities, persists them, and returns the domain profile.
        /// The optional progress reporter receives connecting, mapping, persistence, and completion phases.
        /// </summary>
        Task<PlayerProfile> SyncPlayerProfileAsync(
            string allyCode,
            CancellationToken cancellationToken = default,
            IProgress<PlayerSyncProgress>? progress = null);
    }
}
