#nullable enable

namespace swgoh_command_bridge.Core.Models
{
    /// <summary>
    /// Represents the progress of an incremental scraping operation.
    /// </summary>
    public record ScrapeProgress(
        int Current,
        int Total,
        string CurrentCharacterId,
        string CurrentCharacterName,
        bool Success,
        string? ErrorMessage = null
    );
}