#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using swgoh_command_bridge.Core.Database.Entities;

namespace swgoh_command_bridge.Core.Database.Repositories;

/// <summary>Persists bounded, privacy-safe account sync attempt history.</summary>
public interface ISyncHistoryRepository
{
    Task<long> StartAsync(
        string allyCode,
        DateTime startedUtc,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        long id,
        DateTime completedUtc,
        int characterCount,
        int modCount,
        int warningCount,
        CancellationToken cancellationToken = default);

    Task FinishAsync(
        long id,
        DateTime completedUtc,
        string status,
        string errorSummary,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SyncHistoryEntity>> GetRecentAsync(
        string allyCode,
        int limit = 10,
        CancellationToken cancellationToken = default);
}
