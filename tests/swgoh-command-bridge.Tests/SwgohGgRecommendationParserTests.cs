#nullable enable

using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class SwgohGgRecommendationParserTests
{
    [Fact]
    public void Parse_AllowsAttributeOrderQuoteStyleAndLocalizedText()
    {
        const string html = @"
            <div alt='Speed Set' class='mod-set-image'></div>
            <span class='mod-set-percent'>62,5%</span>
            <h3>slot 2</h3>
            <span data-label='primary' class='mod-stat-name'>Velocidad</span>
            <strong class='mod-stat-percent'>95.2%</strong>";

        var result = new SwgohGgRecommendationParser().Parse(html);

        Assert.True(result.HasRecommendations);
        Assert.Contains(result.Sets, set => set.Name == "Speed" && set.Percentage == 62.5);
        Assert.True(result.PrimaryStats.ContainsKey("Arrow"));
        Assert.Equal("Velocidad", Assert.Single(result.PrimaryStats["Arrow"]).StatName);
        Assert.Equal(95.2, Assert.Single(result.PrimaryStats["Arrow"]).Percentage);
    }

    [Fact]
    public void Parse_SupportsDataSlotAndMissingSectionsWithoutFabricatingData()
    {
        const string html = @"
            <div data-slot='4'>
              <span class='mod-stat-name'>Critical Damage</span>
              <span class='mod-stat-percent'>78%</span>
            </div>";

        var result = new SwgohGgRecommendationParser().Parse(html);

        Assert.False(result.Sets.Count > 0);
        Assert.True(result.HasRecommendations);
        Assert.Contains("Triangle", result.PrimaryStats.Keys);
        Assert.Empty(result.Sets);
    }

    [Fact]
    public void Parse_ReportsNoRecommendationsForUnrelatedMarkup()
    {
        var result = new SwgohGgRecommendationParser().Parse("<html><body>Nothing useful</body></html>");

        Assert.False(result.HasRecommendations);
        Assert.Empty(result.Sets);
        Assert.Empty(result.PrimaryStats);
    }
}
