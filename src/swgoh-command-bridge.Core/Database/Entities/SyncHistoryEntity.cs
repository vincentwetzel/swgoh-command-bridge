#nullable enable

using System;

namespace swgoh_command_bridge.Core.Database.Entities;

/// <summary>Durable, privacy-safe outcome metadata for one account sync attempt.</summary>
public class SyncHistoryEntity
{
    public long Id { get; set; }

    public string AllyCode { get; set; } = string.Empty;

    public DateTime StartedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    /// <summary>One of running, completed, failed, or cancelled.</summary>
    public string Status { get; set; } = "running";

    public int CharacterCount { get; set; }

    public int ModCount { get; set; }

    public int WarningCount { get; set; }

    /// <summary>Privacy-safe failure text; raw exception messages are never persisted.</summary>
    public string? ErrorSummary { get; set; }
}
