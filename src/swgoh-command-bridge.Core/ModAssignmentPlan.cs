#nullable enable

using System.Collections.Generic;

namespace swgoh_command_bridge.Core.Models
{
    /// <summary>
    /// Represents the planned mod assignments for a specific character,
    /// including any recommended mod swaps.
    /// </summary>
    public record ModAssignmentPlan(
        string CharacterId,
        string CharacterName,
        List<AssignedModDetail> AssignedMods,
        List<ModSwapRecommendation> SwapRecommendations
    );
}