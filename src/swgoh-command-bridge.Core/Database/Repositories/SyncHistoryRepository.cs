#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using swgoh_command_bridge.Core.Database.Entities;

namespace swgoh_command_bridge.Core.Database.Repositories;

/// <summary>SQLite implementation for bounded sync outcome history.</summary>
public sealed class SyncHistoryRepository : ISyncHistoryRepository
{
    private const int MaxHistoryPerAccount = 20;
    private readonly AppDbContext _context;

    public SyncHistoryRepository(AppDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<long> StartAsync(
        string allyCode,
        DateTime startedUtc,
        CancellationToken cancellationToken = default)
    {
        var entry = new SyncHistoryEntity
        {
            AllyCode = allyCode.Trim(),
            StartedUtc = startedUtc,
            Status = "running"
        };
        _context.SyncHistory.Add(entry);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await PruneAsync(entry.AllyCode, cancellationToken).ConfigureAwait(false);
        return entry.Id;
    }

    public async Task CompleteAsync(
        long id,
        DateTime completedUtc,
        int characterCount,
        int modCount,
        int warningCount,
        CancellationToken cancellationToken = default)
    {
        var entry = await _context.SyncHistory
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entry == null)
        {
            return;
        }

        entry.CompletedUtc = completedUtc;
        entry.Status = "completed";
        entry.CharacterCount = characterCount;
        entry.ModCount = modCount;
        entry.WarningCount = warningCount;
        entry.ErrorSummary = null;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task FinishAsync(
        long id,
        DateTime completedUtc,
        string status,
        string errorSummary,
        CancellationToken cancellationToken = default)
    {
        var entry = await _context.SyncHistory
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entry == null)
        {
            return;
        }

        entry.CompletedUtc = completedUtc;
        entry.Status = status;
        entry.ErrorSummary = errorSummary.Trim();
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SyncHistoryEntity>> GetRecentAsync(
        string allyCode,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var boundedLimit = Math.Clamp(limit, 1, MaxHistoryPerAccount);
        return await _context.SyncHistory
            .AsNoTracking()
            .Where(entry => entry.AllyCode == allyCode.Trim())
            .OrderByDescending(entry => entry.StartedUtc)
            .ThenByDescending(entry => entry.Id)
            .Take(boundedLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task PruneAsync(string allyCode, CancellationToken cancellationToken)
    {
        var staleEntries = await _context.SyncHistory
            .Where(entry => entry.AllyCode == allyCode)
            .OrderByDescending(entry => entry.StartedUtc)
            .ThenByDescending(entry => entry.Id)
            .Skip(MaxHistoryPerAccount)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (staleEntries.Count == 0)
        {
            return;
        }

        _context.SyncHistory.RemoveRange(staleEntries);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
