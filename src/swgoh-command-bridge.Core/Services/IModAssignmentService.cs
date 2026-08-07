#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services
{
    /// <summary>
    /// Service for calculating optimal mod assignments for characters.
    /// </summary>
    public interface IModAssignmentService
    {
        /// <summary>
        /// Calculates the best mod assignments for a character from the available inventory.
        /// </summary>
        Task<IReadOnlyCollection<GameModEntity>> CalculateOptimalLoadoutAsync(
            string characterId,
            IEnumerable<GameModEntity> availableInventory,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates a loadout and returns completeness, rule validity, and match explanations.
        /// </summary>
        Task<ModLoadoutResult> CalculateOptimalLoadoutResultAsync(
            string characterId,
            IEnumerable<GameModEntity> availableInventory,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates priority-ordered loadouts while reserving each inventory mod at most once.
        /// </summary>
        Task<RosterLoadoutResult> CalculateRosterLoadoutsAsync(
            IEnumerable<CharacterEntity> characters,
            IEnumerable<GameModEntity> availableInventory,
            CancellationToken cancellationToken = default);
    }
}
