#nullable enable

using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class SecondaryStatFilterServiceTests
{
    private const string StatsJson = "[{\"Type\":\"Speed\",\"Value\":15,\"RollCount\":3},{\"Type\":\"OffensePercent\",\"Value\":10,\"RollCount\":2}]";

    [Fact]
    public void Matches_CommaSeparatedCriteria_RequiresEveryStat()
    {
        Assert.True(SecondaryStatFilterService.Matches(StatsJson, "Speed, OffensePercent"));
        Assert.False(SecondaryStatFilterService.Matches(StatsJson, "Speed, Potency"));
    }

    [Fact]
    public void Matches_NumericCriteria_UsesTheRequestedComparison()
    {
        Assert.True(SecondaryStatFilterService.Matches(StatsJson, "Speed>=15, OffensePercent>9"));
        Assert.False(SecondaryStatFilterService.Matches(StatsJson, "Speed>15"));
    }

    [Fact]
    public void TryParse_InvalidCriteriaReturnsHelpfulError()
    {
        var success = SecondaryStatFilterService.TryParse(
            "NotAStat>=fast",
            out _,
            out var error);

        Assert.False(success);
        Assert.NotNull(error);
    }
}
