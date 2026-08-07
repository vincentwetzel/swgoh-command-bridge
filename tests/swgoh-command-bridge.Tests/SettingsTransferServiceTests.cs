#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class SettingsTransferServiceTests
{
    private readonly SettingsTransferService _service = new();

    [Fact]
    public void SerializeAndDeserialize_RoundTripsVersionedSettings()
    {
        var settings = new AppSettings(
            ComlinkBaseUrl: "http://localhost:3000",
            DefaultAllyCode: "123456789",
            Theme: "Dark",
            AutomaticallyCheckForUpdates: false,
            UpgradeThresholds: new List<ModUpgradeThresholdSetting>
            {
                new(5, 4, "Speed", 18, "Competitive", true, 65, "competitive")
            },
            DefaultUpgradeThresholdId: "competitive",
            EnableLocalRecommendationScraping: false);

        var json = _service.Serialize(settings);
        var restored = _service.DeserializeAndValidate(json);

        Assert.Contains("schemaVersion", json);
        Assert.Equal(settings.ComlinkBaseUrl + "/", restored.ComlinkBaseUrl);
        Assert.Equal(settings.DefaultAllyCode, restored.DefaultAllyCode);
        Assert.Equal(settings.Theme, restored.Theme);
        Assert.Equal(settings.UpgradeThresholds, restored.UpgradeThresholds);
        Assert.Equal(settings.DefaultUpgradeThresholdId, restored.DefaultUpgradeThresholdId);
        Assert.False(restored.EnableLocalRecommendationScraping);
    }

    [Fact]
    public void DeserializeAndValidate_AcceptsLegacyPlainSettings()
    {
        var json = "{\"comlinkBaseUrl\":\"http://localhost:3000\",\"defaultAllyCode\":\"123456789\",\"theme\":\"Light\"}";

        var restored = _service.DeserializeAndValidate(json);

        Assert.Equal("123456789", restored.DefaultAllyCode);
        Assert.Equal("Light", restored.Theme);
    }

    [Fact]
    public void Serialize_RemovesEmbeddedUrlCredentials()
    {
        var json = _service.Serialize(new AppSettings(
            ComlinkBaseUrl: "http://user:secret@localhost:3000",
            DefaultAllyCode: "123456789"));

        Assert.DoesNotContain("secret", json);
        var restored = _service.DeserializeAndValidate(json);
        Assert.DoesNotContain("@", restored.ComlinkBaseUrl);
    }

    [Fact]
    public void DeserializeAndValidate_RejectsInvalidAllyCode()
    {
        var json = "{\"schemaVersion\":1,\"settings\":{\"comlinkBaseUrl\":\"http://localhost:3000\",\"defaultAllyCode\":\"bad\"}}";

        var exception = Assert.Throws<InvalidDataException>(
            () => _service.DeserializeAndValidate(json));

        Assert.Contains("nine-digit", exception.Message);
    }

    [Fact]
    public void DeserializeAndValidate_RejectsUnsupportedSchemaVersion()
    {
        var json = "{\"schemaVersion\":99,\"settings\":{\"comlinkBaseUrl\":\"http://localhost:3000\"}}";

        var exception = Assert.Throws<InvalidDataException>(
            () => _service.DeserializeAndValidate(json));

        Assert.Contains("schema version 99", exception.Message);
    }

    [Fact]
    public void DeserializeAndValidate_RejectsInvalidThresholdRules()
    {
        var json = "{\"schemaVersion\":1,\"settings\":{\"comlinkBaseUrl\":\"http://localhost:3000\",\"upgradeThresholds\":[{\"minPips\":0,\"minTier\":4,\"statName\":\"Speed\",\"minValue\":18,\"name\":\"Broken\"}]}}";

        var exception = Assert.Throws<InvalidDataException>(
            () => _service.DeserializeAndValidate(json));

        Assert.Contains("invalid minimum pips", exception.Message);
    }
}
