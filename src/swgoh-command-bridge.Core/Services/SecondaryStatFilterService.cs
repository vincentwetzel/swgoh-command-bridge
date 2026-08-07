#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services;

/// <summary>
/// Parses and evaluates the compact secondary-stat filter syntax used by the inventory screen.
/// </summary>
public static class SecondaryStatFilterService
{
    private static readonly string[] ComparisonTokens = { ">=", "<=", "=", ">", "<" };

    public static bool TryParse(
        string? filter,
        out IReadOnlyList<SecondaryStatCriterion> criteria,
        out string? error)
    {
        var parsed = new List<SecondaryStatCriterion>();
        criteria = parsed;
        error = null;

        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        foreach (var rawTerm in filter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var term = rawTerm.Trim();
            var comparisonToken = ComparisonTokens.FirstOrDefault(
                token => term.Contains(token, StringComparison.Ordinal));
            var comparison = SecondaryStatComparison.Any;
            var statName = term;
            double? threshold = null;

            if (comparisonToken != null)
            {
                var operatorIndex = term.IndexOf(comparisonToken, StringComparison.Ordinal);
                statName = term[..operatorIndex].Trim();
                var rawThreshold = term[(operatorIndex + comparisonToken.Length)..].Trim();

                if (!double.TryParse(
                        rawThreshold,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var parsedThreshold))
                {
                    error = $"Invalid secondary-stat value in '{term}'.";
                    criteria = Array.Empty<SecondaryStatCriterion>();
                    return false;
                }

                threshold = parsedThreshold;
                comparison = comparisonToken switch
                {
                    ">=" => SecondaryStatComparison.GreaterThanOrEqual,
                    "<=" => SecondaryStatComparison.LessThanOrEqual,
                    ">" => SecondaryStatComparison.GreaterThan,
                    "<" => SecondaryStatComparison.LessThan,
                    _ => SecondaryStatComparison.Equal
                };
            }

            if (!Enum.TryParse<StatType>(statName, true, out var statType) || statType == StatType.None)
            {
                error = $"Unknown secondary stat '{statName}'.";
                criteria = Array.Empty<SecondaryStatCriterion>();
                return false;
            }

            parsed.Add(new SecondaryStatCriterion(statType, comparison, threshold));
        }

        return true;
    }

    public static bool Matches(string? secondaryStatsJson, string? filter)
    {
        if (!TryParse(filter, out var criteria, out _))
        {
            return false;
        }

        if (criteria.Count == 0)
        {
            return true;
        }

        List<ModStatSnapshot>? snapshots;
        try
        {
            snapshots = JsonSerializer.Deserialize<List<ModStatSnapshot>>(secondaryStatsJson ?? "[]");
        }
        catch (JsonException)
        {
            return false;
        }

        if (snapshots == null)
        {
            return false;
        }

        return criteria.All(criterion => snapshots.Any(snapshot =>
            Enum.TryParse<StatType>(snapshot.Type, true, out var actualType) &&
            actualType == criterion.StatType &&
            criterion.Matches(snapshot.Value)));
    }
}

public enum SecondaryStatComparison
{
    Any,
    Equal,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

public sealed record SecondaryStatCriterion(
    StatType StatType,
    SecondaryStatComparison Comparison,
    double? Threshold)
{
    public bool Matches(double value)
    {
        var threshold = Threshold.GetValueOrDefault();
        return Comparison switch
        {
            SecondaryStatComparison.Any => true,
            SecondaryStatComparison.Equal => value == threshold,
            SecondaryStatComparison.GreaterThan => value > threshold,
            SecondaryStatComparison.GreaterThanOrEqual => value >= threshold,
            SecondaryStatComparison.LessThan => value < threshold,
            SecondaryStatComparison.LessThanOrEqual => value <= threshold,
            _ => false
        };
    }
}
