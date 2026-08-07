#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services;

/// <summary>
/// Represents the recommendation sections extracted from one public best-mods page.
/// </summary>
public sealed record SwgohGgRecommendationParseResult(
    IReadOnlyList<RecommendedSet> Sets,
    IReadOnlyDictionary<string, IReadOnlyList<RecommendedPrimary>> PrimaryStats)
{
    public bool HasRecommendations => Sets.Count > 0 || PrimaryStats.Count > 0;
}

/// <summary>
/// Parses recommendation markup without coupling network or persistence concerns to HTML details.
/// </summary>
public sealed class SwgohGgRecommendationParser
{
    private const double DefaultSetPopularity = 50.0;
    private const double DefaultPrimaryPopularity = 100.0;
    private const int SectionSearchLimit = 2_000;

    private static readonly Regex OpeningTagRegex = new(
        @"<(?<name>[a-z][a-z0-9]*)\b(?<attributes>[^>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ElementRegex = new(
        @"<(?<name>[a-z][a-z0-9]*)\b(?<attributes>(?=[^>]*\bclass\s*=)[^>]*)>(?<content>.*?)</\k<name>>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex SlotRegex = new(
        @"\bSlot\s*(?<slot>[1-6])\b|\bdata-slot\s*=\s*[""'](?<slotAttribute>[1-6])[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AttributeRegex = new(
        @"\b(?<name>[a-z][a-z0-9-]*)\s*=\s*(?:[""'](?<quoted>[^""']*)[""']|(?<unquoted>[^\s>]+))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PercentRegex = new(
        @"(?<value>\d+(?:[.,]\d+)?)\s*%",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public SwgohGgRecommendationParseResult Parse(string htmlContent)
    {
        ArgumentNullException.ThrowIfNull(htmlContent);

        var elements = ElementRegex.Matches(htmlContent)
            .Cast<Match>()
            .Select(match => new ParsedElement(
                match.Index,
                match.Length,
                GetAttribute(match.Groups["attributes"].Value, "class"),
                HtmlDecode(match.Groups["content"].Value)))
            .ToList();
        var openingTags = OpeningTagRegex.Matches(htmlContent)
            .Cast<Match>()
            .ToList();

        var sets = ParseSets(htmlContent, openingTags, elements);
        var primaryStats = ParsePrimaryStats(htmlContent, elements);

        return new SwgohGgRecommendationParseResult(sets, primaryStats);
    }

    private static IReadOnlyList<RecommendedSet> ParseSets(
        string htmlContent,
        IReadOnlyList<Match> openingTags,
        IReadOnlyList<ParsedElement> elements)
    {
        var sets = new List<RecommendedSet>();

        foreach (var tag in openingTags)
        {
            var attributes = tag.Groups["attributes"].Value;
            var classes = GetAttribute(attributes, "class");
            if (!HasClass(classes, "mod-set-image"))
            {
                continue;
            }

            var setName = GetAttribute(attributes, "alt");
            if (string.IsNullOrWhiteSpace(setName))
            {
                setName = GetAttribute(attributes, "data-set");
            }

            if (string.IsNullOrWhiteSpace(setName))
            {
                continue;
            }

            var searchStart = tag.Index + tag.Length;
            var searchEnd = Math.Min(htmlContent.Length, searchStart + SectionSearchLimit);
            var percentage = ParsePercentage(
                elements.FirstOrDefault(element =>
                    element.Index >= searchStart &&
                    element.Index < searchEnd &&
                    HasClass(element.Classes, "mod-set-percent"))?.Content,
                DefaultSetPopularity);

            var normalizedName = Regex.Replace(
                HtmlDecode(setName),
                @"\s+Set$",
                string.Empty,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
            if (normalizedName.Length > 0)
            {
                sets.Add(new RecommendedSet(normalizedName, percentage));
            }
        }

        return sets
            .GroupBy(set => set.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(set => set.Percentage).First())
            .ToList()
            .AsReadOnly();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<RecommendedPrimary>> ParsePrimaryStats(
        string htmlContent,
        IReadOnlyList<ParsedElement> elements)
    {
        var stats = new Dictionary<string, List<RecommendedPrimary>>(StringComparer.OrdinalIgnoreCase);

        foreach (Match slotMatch in SlotRegex.Matches(htmlContent))
        {
            var slotNumber = slotMatch.Groups["slot"].Success
                ? slotMatch.Groups["slot"].Value
                : slotMatch.Groups["slotAttribute"].Value;
            var slotName = SlotName(slotNumber);
            if (slotName == null)
            {
                continue;
            }

            var sectionEnd = Math.Min(htmlContent.Length, slotMatch.Index + SectionSearchLimit);
            var statElement = elements.FirstOrDefault(element =>
                element.Index >= slotMatch.Index &&
                element.Index < sectionEnd &&
                HasClass(element.Classes, "mod-stat-name"));
            if (statElement == null || string.IsNullOrWhiteSpace(statElement.Content))
            {
                continue;
            }

            var percentageElement = elements.FirstOrDefault(element =>
                element.Index >= statElement.Index &&
                element.Index < sectionEnd &&
                HasClass(element.Classes, "mod-stat-percent"));
            var percentage = ParsePercentage(percentageElement?.Content, DefaultPrimaryPopularity);

            if (!stats.TryGetValue(slotName, out var values))
            {
                values = new List<RecommendedPrimary>();
                stats[slotName] = values;
            }

            values.Add(new RecommendedPrimary(statElement.Content.Trim(), percentage));
        }

        return stats.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<RecommendedPrimary>)pair.Value.AsReadOnly(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static double ParsePercentage(string? content, double fallback)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return fallback;
        }

        var match = PercentRegex.Match(content);
        return match.Success &&
            double.TryParse(
                match.Groups["value"].Value.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var percentage)
            ? percentage
            : fallback;
    }

    private static string? SlotName(string slotNumber) => slotNumber switch
    {
        "1" => "Square",
        "2" => "Arrow",
        "3" => "Diamond",
        "4" => "Triangle",
        "5" => "Circle",
        "6" => "Cross",
        _ => null
    };

    private static string GetAttribute(string attributes, string name)
    {
        foreach (Match match in AttributeRegex.Matches(attributes))
        {
            if (string.Equals(match.Groups["name"].Value, name, StringComparison.OrdinalIgnoreCase))
            {
                return match.Groups["quoted"].Success
                    ? match.Groups["quoted"].Value
                    : match.Groups["unquoted"].Value;
            }
        }

        return string.Empty;
    }

    private static bool HasClass(string classes, string expectedClass) =>
        classes.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Any(value => string.Equals(value, expectedClass, StringComparison.OrdinalIgnoreCase));

    private static string HtmlDecode(string value) =>
        WebUtility.HtmlDecode(Regex.Replace(value, "<[^>]+>", " ")).Trim();

    private sealed record ParsedElement(int Index, int Length, string Classes, string Content);
}
