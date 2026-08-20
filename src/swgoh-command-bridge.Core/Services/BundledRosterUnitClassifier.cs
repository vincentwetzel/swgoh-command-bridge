#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace swgoh_command_bridge.Core.Services;

/// <summary>
/// Identifies ships using the bundled, versioned SWGOH ship catalog.
/// </summary>
public interface IRosterUnitClassifier
{
    /// <summary>Returns whether a roster unit is a ship.</summary>
    bool IsShip(string unitId);
}

/// <summary>
/// Classifies roster units without relying on names, which can be localized.
/// </summary>
public sealed class BundledRosterUnitClassifier : IRosterUnitClassifier
{
    private const string ShipsResourceSuffix = ".Assets.CharacterCatalog.swgoh-ships.json";
    private readonly Lazy<HashSet<string>> _shipIds = new(ReadShipIds);

    /// <inheritdoc />
    public bool IsShip(string unitId)
    {
        return !string.IsNullOrWhiteSpace(unitId) && _shipIds.Value.Contains(unitId);
    }

    private static HashSet<string> ReadShipIds()
    {
        var assembly = typeof(BundledRosterUnitClassifier).Assembly;
        var resourceName = Array.Find(
            assembly.GetManifestResourceNames(),
            name => name.EndsWith(ShipsResourceSuffix, StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            throw new InvalidDataException("The bundled ship catalog could not be found.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException("The bundled ship catalog could not be opened.");
        using var document = JsonDocument.Parse(stream);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ship in document.RootElement.EnumerateArray())
        {
            if (ship.TryGetProperty("base_id", out var id) &&
                id.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(id.GetString()))
            {
                ids.Add(id.GetString()!);
            }
        }

        return ids;
    }
}
