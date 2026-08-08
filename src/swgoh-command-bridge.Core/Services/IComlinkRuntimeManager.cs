#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace swgoh_command_bridge.Core.Services;

public interface IComlinkRuntimeManager : IDisposable
{
    Task<ComlinkRuntimeResult> EnsureReadyAsync(
        Uri requestedBaseAddress,
        IProgress<ComlinkRuntimeProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

public sealed record ComlinkRuntimeProgress(string Message, double? Percent = null);

public sealed record ComlinkRuntimeResult(Uri BaseAddress, bool ManagedLocally);
