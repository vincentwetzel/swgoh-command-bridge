#nullable enable

using System;
using System.Linq;

namespace swgoh_command_bridge.Core.Models;

/// <summary>Normalizes and validates the nine-digit ally-code format expected by Comlink.</summary>
public static class AllyCodeValidator
{
    public static bool TryNormalize(
        string? value,
        out string normalized,
        out string errorMessage)
    {
        normalized = value?.Trim() ?? string.Empty;
        errorMessage = string.Empty;

        if (normalized.Length != 9 || normalized.Any(character => character < '0' || character > '9'))
        {
            errorMessage = "Enter a valid nine-digit ally code.";
            normalized = string.Empty;
            return false;
        }

        return true;
    }

    public static string NormalizeOrThrow(string? value)
    {
        if (TryNormalize(value, out var normalized, out var errorMessage))
        {
            return normalized;
        }

        throw new ArgumentException(errorMessage, nameof(value));
    }
}
