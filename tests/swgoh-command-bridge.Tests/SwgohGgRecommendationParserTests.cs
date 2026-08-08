#nullable enable

using swgoh_command_bridge.Core.Services;
using swgoh_command_bridge.Tests.Fixtures;
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
    public void Parse_UsesSectionBoundariesForAdjacentSetsAndSlots()
    {
        var result = new SwgohGgRecommendationParser().Parse(
            RecommendationPageFixtures.MultipleSetsAndSlots);

        Assert.Equal(2, result.Sets.Count);
        Assert.Contains(result.Sets, set => set.Name == "Speed" && set.Percentage == 88);
        Assert.Contains(result.Sets, set => set.Name == "Health" && set.Percentage == 55);
        Assert.Equal(90, Assert.Single(result.PrimaryStats["Arrow"]).Percentage);
        Assert.Equal(76, Assert.Single(result.PrimaryStats["Triangle"]).Percentage);
    }

    [Fact]
    public void Parse_HandlesNestedLocalizedSetAndPrimarySections()
    {
        var result = new SwgohGgRecommendationParser().Parse(
            RecommendationPageFixtures.NestedLocalizedSections);

        Assert.Contains(result.Sets, set => set.Name == "Potency" && set.Percentage == 71.5);
        Assert.Equal("Primaria", Assert.Single(result.PrimaryStats["Circle"]).StatName);
        Assert.Equal(64.25, Assert.Single(result.PrimaryStats["Circle"]).Percentage);
    }

    [Fact]
    public void Parse_HandlesFullPageNoiseDuplicateSetsAndMixedSlotMarkup()
    {
        var result = new SwgohGgRecommendationParser().Parse(
            RecommendationPageFixtures.FullPageVariation);

        Assert.Equal(2, result.Sets.Count);
        Assert.Contains(result.Sets, set => set.Name == "Speed" && set.Percentage == 91);
        Assert.Contains(result.Sets, set => set.Name == "Health" && set.Percentage == 55.5);
        Assert.Equal(95, Assert.Single(result.PrimaryStats["Arrow"]).Percentage);
        Assert.Equal(78.25, Assert.Single(result.PrimaryStats["Triangle"]).Percentage);
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
