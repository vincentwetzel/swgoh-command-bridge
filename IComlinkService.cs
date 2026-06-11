#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace swgoh_command_bridge.Core.Services
{
    /// <summary>
    /// Service interface for interacting with the local swgoh-comlink API.
    /// </summary>
    public interface IComlinkService
    {
        /// <summary>
        /// Fetches raw player profile data for the specified ally code.
        /// </summary>
        Task<string> FetchPlayerRawAsync(string allyCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetches the raw game metadata.
        /// </summary>
        Task<string> FetchMetaDataRawAsync(CancellationToken cancellationToken = default);
    }
}