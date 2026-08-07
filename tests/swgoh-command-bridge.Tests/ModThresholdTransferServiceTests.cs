#nullable enable

using System;
using System.IO;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class ModThresholdTransferServiceTests
{
    private readonly ModThresholdTransferService _service = new();

    [Fact]
    public void SerializeAndDeserialize_RoundTripsVersionedThresholds()
    {
        var source = new[]
        {
            new ModUpgradeThreshold("competitive", "Competitive", 5, 4, 18, true, 65)
        };

        var json = _service.Serialize(source);
        var restored = _service.DeserializeAndValidate(json);

        Assert.Contains("schemaVersion", json);
        var threshold = Assert.Single(restored);
        Assert.Equal(source[0], threshold);
    }

    [Fact]
    public void DeserializeAndValidate_AcceptsLegacyArrayAndGeneratesMissingIds()
    {
        var json = "[{\"minPips\":5,\"minTier\":4,\"statName\":\"Speed\",\"minValue\":15,\"name\":\"Legacy\",\"upgradeOnlyWithSpeed\":true,\"minimumEfficiency\":0}]";

        var restored = _service.DeserializeAndValidate(json);

        var threshold = Assert.Single(restored);
        Assert.Equal("Legacy", threshold.Name);
        Assert.False(string.IsNullOrWhiteSpace(threshold.Id));
    }

    [Fact]
    public void DeserializeAndValidate_RejectsUnsupportedSchemaVersion()
    {
        var json = "{\"schemaVersion\":99,\"thresholds\":[]}";

        var exception = Assert.Throws<InvalidDataException>(() => _service.DeserializeAndValidate(json));

        Assert.Contains("schema version 99", exception.Message);
    }

    [Fact]
    public void DeserializeAndValidate_RejectsDuplicateIdsBeforeImport()
    {
        var json = "{\"schemaVersion\":1,\"thresholds\":[" +
            "{\"minPips\":5,\"minTier\":4,\"statName\":\"Speed\",\"minValue\":15,\"name\":\"First\",\"id\":\"same\"}," +
            "{\"minPips\":5,\"minTier\":4,\"statName\":\"Speed\",\"minValue\":20,\"name\":\"Second\",\"id\":\"same\"}]}";

        var exception = Assert.Throws<InvalidDataException>(() => _service.DeserializeAndValidate(json));

        Assert.Contains("duplicate ID", exception.Message);
    }
}
