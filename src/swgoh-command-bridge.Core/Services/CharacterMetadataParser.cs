#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace swgoh_command_bridge.Core.Services;

/// <summary>Extracts character display names from tolerant Comlink metadata payloads.</summary>
public sealed class CharacterMetadataParser
{
    private static readonly string[] IdNames =
        { "baseId", "definitionId", "unitDefId", "characterId", "unitId" };

    private static readonly string[] DisplayNameNames =
        { "name", "displayName", "characterName", "unitName", "localizedName" };

    public IReadOnlyDictionary<string, string> Parse(string rawJson)
    {
        ArgumentNullException.ThrowIfNull(rawJson);

        using var document = JsonDocument.Parse(rawJson);
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Visit(document.RootElement, names, 0);
        return names;
    }

    private static void Visit(
        JsonElement element,
        IDictionary<string, string> names,
        int depth)
    {
        if (depth > 10)
        {
            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Visit(item, names, depth + 1);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var id = GetString(element, IdNames);
        var displayName = GetString(element, DisplayNameNames);
        if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(displayName))
        {
            var normalizedId = NormalizeId(id);
            if (!string.IsNullOrWhiteSpace(normalizedId))
            {
                names[normalizedId] = displayName.Trim();
            }
        }

        foreach (var property in element.EnumerateObject())
        {
            Visit(property.Value, names, depth + 1);
        }
    }

    private static string? GetString(JsonElement parent, IReadOnlyList<string> names)
    {
        foreach (var name in names)
        {
            if (!parent.TryGetProperty(name, out var value))
            {
                foreach (var property in parent.EnumerateObject())
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        break;
                    }
                }
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static string NormalizeId(string value) =>
        value.Split(':', 2)[0].Trim();
}
