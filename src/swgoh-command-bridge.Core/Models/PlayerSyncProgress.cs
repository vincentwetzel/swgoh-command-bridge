#nullable enable

namespace swgoh_command_bridge.Core.Models;

/// <summary>Describes a user-visible phase of a Comlink account sync.</summary>
public sealed record PlayerSyncProgress(
    string Phase,
    string Message,
    int CompletedSteps,
    int TotalSteps);
