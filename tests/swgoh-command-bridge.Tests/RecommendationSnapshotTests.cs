#nullable enable

using System;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class RecommendationSnapshotTests
{
    [Fact]
    public void FromEntity_ProducesCaseInsensitiveCanonicalRecommendationData()
    {
        var entity = new SwgohGgRecommendationEntity
        {
            CharacterId = "DARTHTRAYA",
            PlayerAllyCode = "123456789",
            Source = "community-cache",
            RecommendationSchemaVersion = 4,
            SourceUrl = "https://example.test/recommendations/darthtraya",
            LastUpdatedUtc = new DateTime(2026, 8, 7, 12, 30, 0, DateTimeKind.Utc),
            PopularityPercentage = 62.5,
            SetRecommendationsJson = "[{\"name\":\"Speed\",\"percentage\":62.5}]",
            PrimaryStatsJson = "{\"Slot 2\":[{\"statName\":\"Speed\",\"percentage\":95.2}]}"
        };

        var snapshot = RecommendationSnapshot.FromEntity(entity);

        Assert.Equal(entity.CharacterId, snapshot.CharacterId);
        Assert.Equal(entity.PlayerAllyCode, snapshot.PlayerAllyCode);
        Assert.Equal("community-cache", snapshot.Source);
        Assert.Equal(4, snapshot.SchemaVersion);
        Assert.Equal(entity.SourceUrl, snapshot.SourceUrl);
        Assert.Equal(entity.LastUpdatedUtc, snapshot.ScrapedAtUtc);
        Assert.Equal(62.5, snapshot.PopularityPercentage);
        Assert.Contains(snapshot.Sets, set => set.Name == "Speed");
        Assert.True(snapshot.PrimaryStats.ContainsKey("slot 2"));
        Assert.Equal("Speed", Assert.Single(snapshot.PrimaryStats["SLOT 2"]).StatName);
    }

    [Fact]
    public void FromEntity_UsesSafeDefaultsForLegacyMetadata()
    {
        var entity = new SwgohGgRecommendationEntity
        {
            Source = string.Empty,
            RecommendationSchemaVersion = 0,
            SourceUrl = null!,
            SetRecommendationsJson = "[]",
            PrimaryStatsJson = "{}"
        };

        var snapshot = RecommendationSnapshot.FromEntity(entity);

        Assert.Equal("swgoh.gg", snapshot.Source);
        Assert.Equal(string.Empty, snapshot.CharacterId);
        Assert.Equal(string.Empty, snapshot.PlayerAllyCode);
        Assert.Equal(1, snapshot.SchemaVersion);
        Assert.Equal(string.Empty, snapshot.SourceUrl);
        Assert.Empty(snapshot.Sets);
        Assert.Empty(snapshot.PrimaryStats);
    }
}
