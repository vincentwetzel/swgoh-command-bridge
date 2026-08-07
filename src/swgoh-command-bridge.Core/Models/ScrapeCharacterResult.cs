#nullable enable

namespace swgoh_command_bridge.Core.Models;

/// <summary>
/// Describes the outcome of a single community-recommendation refresh.
/// </summary>
public sealed record ScrapeCharacterResult(
    bool Success,
    string? ErrorMessage = null,
    bool SkippedFreshData = false);
