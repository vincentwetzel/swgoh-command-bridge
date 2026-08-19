#nullable enable

using System;
using System.Collections.Generic;

namespace swgoh_command_bridge.Core.Models;

/// <summary>Schema and source metadata for the global preferred-mod dataset.</summary>
public sealed record PreferredModsDataset(
    int SchemaVersion,
    string DatasetVersion,
    DateTimeOffset GeneratedAtUtc,
    PreferredModsSource Source,
    IReadOnlyList<PreferredCharacterRecommendation> Characters);

/// <summary>Describes the GAC population used to generate a dataset.</summary>
public sealed record PreferredModsSource(
    string GameMode,
    IReadOnlyList<string> Seasons,
    int AccountCount,
    int ObservationCount);

/// <summary>Preferred sets and slot primaries for one character.</summary>
public sealed record PreferredCharacterRecommendation(
    string CharacterId,
    int SampleSize,
    PreferredConfidence Confidence,
    IReadOnlyList<PreferredSetupPattern> Setups,
    IReadOnlyList<PreferredSlotRecommendation> Slots,
    IReadOnlyList<PreferredModQualityProfile> QualityProfiles);

/// <summary>One observed complete mod build pattern.</summary>
public sealed record PreferredSetupPattern(
    double Share,
    IReadOnlyList<PreferredSetCount> Sets,
    IReadOnlyList<PreferredSetupSlotPrimary> SlotPrimaries);

/// <summary>Number of mods belonging to a set in a complete build pattern.</summary>
public sealed record PreferredSetCount(ModSet Set, int Count);

/// <summary>Primary selected for a slot in a complete build pattern.</summary>
public sealed record PreferredSetupSlotPrimary(ModSlot Slot, StatType PrimaryStat);

/// <summary>Population distribution of primary stats for one character slot.</summary>
public sealed record PreferredSlotRecommendation(
    ModSlot Slot,
    IReadOnlyList<PreferredPrimaryOption> Options);

/// <summary>One primary-stat option in a slot distribution.</summary>
public sealed record PreferredPrimaryOption(
    StatType PrimaryStat,
    double Share,
    int Observations,
    PreferredRecommendationStatus Status);

/// <summary>
/// Aggregate quality information reserved for future farming recommendations.
/// Speed values are absolute mod-secondary values, not percentages.
/// </summary>
public sealed record PreferredModQualityProfile(
    ModSet Set,
    ModSlot Slot,
    StatType PrimaryStat,
    int SampleSize,
    double MedianSpeed,
    double UpperQuartileSpeed,
    double HighPercentileSpeed);

/// <summary>Confidence in a character recommendation's supporting population.</summary>
public enum PreferredConfidence
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3
}

/// <summary>How strongly a primary-stat option should be presented.</summary>
public enum PreferredRecommendationStatus
{
    NoData = 0,
    Inconclusive = 1,
    Preferred = 2,
    ViableAlternative = 3,
    LessCommon = 4
}

/// <summary>Small GitHub-hosted pointer to a versioned dataset payload.</summary>
public sealed record PreferredModsManifest(
    int SchemaVersion,
    string DatasetVersion,
    DateTimeOffset GeneratedAtUtc,
    Uri DatasetUrl,
    string DatasetSha256);

/// <summary>Result of a silent preferred-mod update attempt.</summary>
public sealed record PreferredModsRefreshResult(
    PreferredModsRefreshStatus Status,
    string Message,
    PreferredModsDatasetInfo DatasetInfo);

public enum PreferredModsRefreshStatus
{
    Updated = 0,
    NotDue = 1,
    Current = 2,
    Failed = 3,
    Disabled = 4
}

/// <summary>Safe, concise metadata suitable for UI and diagnostics.</summary>
public sealed record PreferredModsDatasetInfo(
    string DatasetVersion,
    DateTimeOffset GeneratedAtUtc,
    int AccountCount,
    int CharacterCount,
    string Source)
{
    public string CompactSummary =>
        $"Updated {GeneratedAtUtc.ToLocalTime():MMM d} · {AccountCount:N0} top GAC accounts";
}
