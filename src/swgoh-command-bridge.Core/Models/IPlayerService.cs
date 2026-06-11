#nullable enable

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
    }
}