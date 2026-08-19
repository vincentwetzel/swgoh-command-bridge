#nullable enable

using System;

namespace swgoh_command_bridge.Core.Services;

/// <summary>
/// Preserves the game-provided character name. When no authoritative name has
/// been synchronized, it deliberately shows the raw game ID rather than
/// attempting to infer punctuation or words from that ID.
/// </summary>
public static class CharacterNameFormatter
{
    public static string Format(string characterId, string? existingName = null)
    {
        if (!string.IsNullOrWhiteSpace(existingName) &&
            !string.Equals(existingName.Trim(), characterId, StringComparison.OrdinalIgnoreCase))
        {
            return existingName.Trim();
        }

        return characterId.Split(':', 2)[0].Trim();
    }
}

