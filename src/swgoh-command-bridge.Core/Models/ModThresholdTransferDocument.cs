#nullable enable

using System.Collections.Generic;

namespace swgoh_command_bridge.Core.Models;

/// <summary>
/// Versioned portable representation of the user-defined mod threshold collection.
/// </summary>
public sealed record ModThresholdTransferDocument(
    int SchemaVersion,
    List<ModUpgradeThresholdSetting> Thresholds);
