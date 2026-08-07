#nullable enable

namespace swgoh_command_bridge.Core.Models;

/// <summary>Versioned wrapper for portable application settings.</summary>
public sealed record SettingsTransferDocument(
    int SchemaVersion,
    AppSettings? Settings);
