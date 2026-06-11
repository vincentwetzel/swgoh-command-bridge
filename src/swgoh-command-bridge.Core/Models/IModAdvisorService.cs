#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services
{
    /// <summary>
    /// Service interface for analyzing mods and determining upgrade, swap, or sell actions.
    /// </summary>
    public interface IModAdvisorService
    {
        /// <summary>
        /// Analyzes a single mod against user thresholds and character context to return a recommendation.
        /// </summary>
        Task<ModRecommendation> AnalyzeModAsync(
            GameMod mod,
            ModUpgradeThreshold threshold,
            IEnumerable<Character> characters,
            CancellationToken cancellationToken = default);
    }
}