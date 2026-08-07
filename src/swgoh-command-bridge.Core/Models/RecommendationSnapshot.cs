#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using swgoh_command_bridge.Core.Database.Entities;

namespace swgoh_command_bridge.Core.Models;

/// <summary>
/// Canonical community recommendation data shared by scraping, assignment, and UI layers.
/// </summary>
public sealed record RecommendationSnapshot(
    string Source,
    int SchemaVersion,
    string SourceUrl,
    DateTime ScrapedAtUtc,
    double PopularityPercentage,
    IReadOnlyList<RecommendedSet> Sets,
    IReadOnlyDictionary<string, IReadOnlyList<RecommendedPrimary>> PrimaryStats)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static RecommendationSnapshot FromEntity(SwgohGgRecommendationEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var sets = JsonSerializer.Deserialize<List<RecommendedSet>>(
            entity.SetRecommendationsJson,
            SerializerOptions) ?? new List<RecommendedSet>();
        var parsedPrimaries = JsonSerializer.Deserialize<Dictionary<string, List<RecommendedPrimary>>>(
            entity.PrimaryStatsJson,
            SerializerOptions) ?? new Dictionary<string, List<RecommendedPrimary>>();
        var primaryStats = new Dictionary<string, IReadOnlyList<RecommendedPrimary>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var pair in parsedPrimaries)
        {
            primaryStats[pair.Key] = pair.Value;
        }

        return new RecommendationSnapshot(
            string.IsNullOrWhiteSpace(entity.Source) ? "swgoh.gg" : entity.Source,
            entity.RecommendationSchemaVersion <= 0 ? 1 : entity.RecommendationSchemaVersion,
            entity.SourceUrl ?? string.Empty,
            entity.LastUpdatedUtc,
            entity.PopularityPercentage,
            sets,
            primaryStats);
    }
}
