#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services;

/// <summary>Input to the server-side preferred-mod aggregation step.</summary>
public sealed record PreferredModsObservation(PlayerProfile Profile, double Weight = 1);

/// <summary>Backend-owned rules for confidence and alternative classification.</summary>
public sealed record PreferredModsAggregationOptions(
    int MinimumSampleSize = 30,
    int HighConfidenceSampleSize = 150,
    int MediumConfidenceSampleSize = 75,
    double ViableAlternativeGap = 0.20,
    double HighPercentile = 0.90);

/// <summary>
/// Aggregates equipped mod observations into the portable preferred-mod
/// contract. Fetching leaderboards and profiles remains outside this class.
/// </summary>
public sealed class PreferredModsAggregator
{
    public PreferredModsDataset Aggregate(
        IEnumerable<PreferredModsObservation> observations,
        string datasetVersion,
        PreferredModsSource source,
        PreferredModsAggregationOptions? options = null,
        DateTimeOffset? generatedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetVersion);
        ArgumentNullException.ThrowIfNull(source);
        options ??= new PreferredModsAggregationOptions();
        ValidateOptions(options);

        var characters = observations
            .Where(observation => observation.Profile != null && observation.Weight > 0)
            .SelectMany(observation => observation.Profile.Characters.Select(character =>
                new CharacterObservation(character, observation.Weight)))
            .Where(observation => observation.Character.EquippedMods.Count > 0)
            .GroupBy(observation => observation.Character.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => AggregateCharacter(group.ToList(), options))
            .ToList()
            .AsReadOnly();

        return new PreferredModsDataset(
            PreferredModsDatasetService.SupportedSchemaVersion,
            datasetVersion,
            generatedAtUtc ?? DateTimeOffset.UtcNow,
            source,
            characters);
    }

    private static PreferredCharacterRecommendation AggregateCharacter(
        IReadOnlyList<CharacterObservation> observations,
        PreferredModsAggregationOptions options)
    {
        var sampleSize = observations.Count;
        var slots = Enum.GetValues<ModSlot>()
            .Select(slot => AggregateSlot(slot, observations, options))
            .Where(slot => slot.Options.Count > 0)
            .ToList()
            .AsReadOnly();
        var setups = observations
            .Where(observation => observation.Character.EquippedMods.Count == Enum.GetValues<ModSlot>().Length &&
                                  observation.Character.EquippedMods.Values.All(HasKnownPrimary))
            .GroupBy(observation => BuildSetupKey(observation.Character), StringComparer.Ordinal)
            .Select(group => new
            {
                Weight = group.Sum(item => item.Weight),
                Pattern = ToSetupPattern(group.First().Character)
            })
            .OrderByDescending(item => item.Weight)
            .ThenBy(item => string.Join("|", item.Pattern.Sets.Select(set => set.Set)))
            .ToList();
        var setupWeight = setups.Sum(item => item.Weight);
        IReadOnlyList<PreferredSetupPattern> normalizedSetups = setupWeight <= 0
            ? Array.Empty<PreferredSetupPattern>()
            : setups.Select(item => item.Pattern with { Share = item.Weight / setupWeight })
                .ToList()
                .AsReadOnly();
        var qualityProfiles = AggregateQualityProfiles(observations, options.HighPercentile);

        return new PreferredCharacterRecommendation(
            observations[0].Character.Id,
            sampleSize,
            ClassifyConfidence(sampleSize, options),
            normalizedSetups,
            slots,
            qualityProfiles);
    }

    private static PreferredSlotRecommendation AggregateSlot(
        ModSlot slot,
        IReadOnlyList<CharacterObservation> observations,
        PreferredModsAggregationOptions options)
    {
        var values = observations
            .Select(observation => new
            {
                Observation = observation,
                Mod = observation.Character.EquippedMods.GetValueOrDefault(slot)
            })
            .Where(item => item.Mod != null && HasKnownPrimary(item.Mod))
            .ToList();
        var totalWeight = values.Sum(item => item.Observation.Weight);
        if (totalWeight <= 0)
        {
            return new PreferredSlotRecommendation(slot, Array.Empty<PreferredPrimaryOption>());
        }

        var grouped = values
            .GroupBy(item => item.Mod!.Primary.Type)
            .Select(group => new
            {
                Primary = group.Key,
                Weight = group.Sum(item => item.Observation.Weight),
                Observations = group.Count()
            })
            .OrderByDescending(item => item.Weight)
            .ThenBy(item => item.Primary)
            .ToList();
        var sufficientData = values.Count >= options.MinimumSampleSize;
        var leaderShare = grouped[0].Weight / totalWeight;
        var primaryOptions = grouped
            .Select((item, index) => new PreferredPrimaryOption(
                item.Primary,
                item.Weight / totalWeight,
                item.Observations,
                ClassifyPrimaryStatus(
                    index,
                    item.Weight / totalWeight,
                    leaderShare,
                    sufficientData,
                    options.ViableAlternativeGap)))
            .ToList()
            .AsReadOnly();
        return new PreferredSlotRecommendation(slot, primaryOptions);
    }

    private static IReadOnlyList<PreferredModQualityProfile> AggregateQualityProfiles(
        IReadOnlyList<CharacterObservation> observations,
        double highPercentile)
    {
        return observations
            .SelectMany(observation => observation.Character.EquippedMods.Values.Select(mod => new
            {
                observation.Weight,
                Mod = mod,
                Speed = mod.Secondaries.FirstOrDefault(stat => stat.Type == StatType.Speed)?.Value ?? 0
            }))
            .Where(item => HasKnownPrimary(item.Mod))
            .GroupBy(item => (item.Mod.Set, item.Mod.Slot, item.Mod.Primary.Type))
            .Select(group =>
            {
                var speeds = group
                    .Select(item => item.Speed)
                    .OrderBy(value => value)
                    .ToList();
                return new PreferredModQualityProfile(
                    group.Key.Set,
                    group.Key.Slot,
                    group.Key.Type,
                    speeds.Count,
                    Percentile(speeds, 0.50),
                    Percentile(speeds, 0.75),
                    Percentile(speeds, highPercentile));
            })
            .OrderBy(profile => profile.Set)
            .ThenBy(profile => profile.Slot)
            .ThenBy(profile => profile.PrimaryStat)
            .ToList()
            .AsReadOnly();
    }

    private static PreferredSetupPattern ToSetupPattern(Character character)
    {
        var sets = character.EquippedMods.Values
            .GroupBy(mod => mod.Set)
            .OrderBy(group => group.Key)
            .Select(group => new PreferredSetCount(group.Key, group.Count()))
            .ToList()
            .AsReadOnly();
        var primaries = character.EquippedMods
            .OrderBy(pair => pair.Key)
            .Select(pair => new PreferredSetupSlotPrimary(pair.Key, pair.Value.Primary.Type))
            .ToList()
            .AsReadOnly();
        return new PreferredSetupPattern(0, sets, primaries);
    }

    private static string BuildSetupKey(Character character) => string.Join(
        "|",
        character.EquippedMods
            .OrderBy(pair => pair.Key)
            .Select(pair => $"{(int)pair.Key}:{(int)pair.Value.Set}:{(int)pair.Value.Primary.Type}"));

    private static PreferredConfidence ClassifyConfidence(
        int sampleSize,
        PreferredModsAggregationOptions options) =>
        sampleSize >= options.HighConfidenceSampleSize
            ? PreferredConfidence.High
            : sampleSize >= options.MediumConfidenceSampleSize
                ? PreferredConfidence.Medium
                : sampleSize >= options.MinimumSampleSize
                    ? PreferredConfidence.Low
                    : PreferredConfidence.None;

    private static PreferredRecommendationStatus ClassifyPrimaryStatus(
        int index,
        double share,
        double leaderShare,
        bool sufficientData,
        double viableAlternativeGap)
    {
        if (!sufficientData)
        {
            return PreferredRecommendationStatus.Inconclusive;
        }

        if (index == 0)
        {
            return PreferredRecommendationStatus.Preferred;
        }

        return leaderShare - share <= viableAlternativeGap
            ? PreferredRecommendationStatus.ViableAlternative
            : PreferredRecommendationStatus.LessCommon;
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var index = (values.Count - 1) * percentile;
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        return lower == upper
            ? values[lower]
            : values[lower] + ((values[upper] - values[lower]) * (index - lower));
    }

    private static bool HasKnownPrimary(GameMod mod) =>
        mod.Primary.Type != StatType.None && Enum.IsDefined(mod.Primary.Type);

    private static void ValidateOptions(PreferredModsAggregationOptions options)
    {
        if (options.MinimumSampleSize <= 0 || options.MediumConfidenceSampleSize < options.MinimumSampleSize ||
            options.HighConfidenceSampleSize < options.MediumConfidenceSampleSize ||
            options.ViableAlternativeGap is < 0 or > 1 || options.HighPercentile is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Preferred-mod aggregation options are invalid.");
        }
    }

    private sealed record CharacterObservation(Character Character, double Weight);
}
