#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class ModAdvisorDecisionMatrixTests
{
    public static IEnumerable<object[]> DecisionCases()
    {
        yield return new object[]
        {
            "hard rarity floor",
            CreateMod("low-pip", 1, 4, 5, 5, new ModStat(StatType.Speed, 40)),
            CreateThreshold(5, 1, 10),
            new List<Character>(),
            ModRecommendationAction.Sell,
            "rarity"
        };
        yield return new object[]
        {
            "impossible level-up potential",
            CreateMod("impossible", 1, 5, 5, 5),
            CreateThreshold(5, 5, 45),
            new List<Character>(),
            ModRecommendationAction.Sell,
            "can reach at most"
        };
        yield return new object[]
        {
            "viable level-up",
            CreateMod("level", 1, 5, 1, 1, new ModStat(StatType.Speed, 15)),
            CreateThreshold(5, 1, 10),
            new List<Character>(),
            ModRecommendationAction.LevelUp,
            "Level up"
        };
        yield return new object[]
        {
            "tier slicing",
            CreateMod("tier", 1, 5, 5, 1, new ModStat(StatType.Speed, 10)),
            CreateThreshold(5, 4, 10),
            new List<Character>(),
            ModRecommendationAction.Slice,
            "below the required tier"
        };
        yield return new object[]
        {
            "five-dot slicing",
            CreateMod("five-dot", 1, 5, 15, 5, new ModStat(StatType.Speed, 20)),
            CreateThreshold(5, 5, 15),
            new List<Character>(),
            ModRecommendationAction.Slice,
            "advanced to 6-dot"
        };
        yield return new object[]
        {
            "threshold keep",
            CreateMod("keep", 1, 6, 15, 5, new ModStat(StatType.Speed, 20)),
            CreateThreshold(5, 5, 15),
            new List<Character>(),
            ModRecommendationAction.Keep,
            "meet the active threshold"
        };
        yield return new object[]
        {
            "sell without replacement",
            CreateMod("sell", 1, 5, 15, 5, new ModStat(StatType.Speed, 2)),
            CreateThreshold(5, 5, 15),
            new List<Character>(),
            ModRecommendationAction.Sell,
            "no compatible higher-priority"
        };
        yield return new object[]
        {
            "compatible priority swap",
            CreateMod("candidate", 1, 5, 15, 5, new ModStat(StatType.Speed, 12)),
            CreateThreshold(5, 5, 15),
            new List<Character>
            {
                new(
                    "CHARACTER",
                    "Priority Target",
                    85,
                    12,
                    0,
                    15000,
                    10,
                    new Dictionary<ModSlot, GameMod>
                    {
                        [ModSlot.Square] = CreateMod(
                            "equipped",
                            1,
                            5,
                            15,
                            5,
                            new ModStat(StatType.Speed, 8),
                            "CHARACTER")
                    })
            },
            ModRecommendationAction.Swap,
            "highest-priority compatible target"
        };
    }

    [Theory]
    [MemberData(nameof(DecisionCases))]
    public async Task AnalyzeModAsync_CoversDecisionMatrix(
        string caseName,
        GameMod mod,
        ModUpgradeThreshold threshold,
        List<Character> characters,
        ModRecommendationAction expectedAction,
        string expectedReason)
    {
        var service = new ModAdvisorService(
            NullLogger<ModAdvisorService>.Instance,
            new ModMechanicsService());

        var recommendation = await service.AnalyzeModAsync(mod, threshold, characters);

        Assert.Equal(expectedAction, recommendation.Action);
        Assert.Contains(expectedReason, recommendation.Reason);
    }

    private static ModUpgradeThreshold CreateThreshold(int rarity, int tier, int speed) =>
        new("matrix", "Matrix", rarity, tier, speed, true, 0);

    private static GameMod CreateMod(
        string id,
        int slot,
        int pips,
        int level,
        int tier,
        ModStat? speed = null,
        string? equippedUnitId = null) =>
        new(
            id,
            level,
            pips,
            tier,
            (ModSlot)slot,
            ModSet.Health,
            new ModStat(StatType.Offense, 0.5),
            speed == null ? new List<ModStat>() : new List<ModStat> { speed },
            equippedUnitId);
}
