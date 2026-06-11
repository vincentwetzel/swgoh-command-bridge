#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using swgoh_command_bridge.Core.Database.Entities;

namespace swgoh_command_bridge.Core.Services
{
    /// <summary>
    /// Service for calculating optimal mod assignments for characters based on inventory and scraped swgoh.gg guidelines.
    /// </summary>
    public interface IModAssignmentService
    {
        /// <summary>
        /// Calculates the best mod assignments for a specific character from the available inventory.
        /// </summary>
        Task<IReadOnlyCollection<GameModEntity>> CalculateOptimalLoadoutAsync(
            string characterId, 
            IEnumerable<GameModEntity> availableInventory, 
            CancellationToken cancellationToken = default);
    }
}