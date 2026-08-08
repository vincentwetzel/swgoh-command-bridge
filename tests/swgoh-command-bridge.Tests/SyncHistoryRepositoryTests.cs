#nullable enable

using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Database.Repositories;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class SyncHistoryRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    public SyncHistoryRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task StartAndCompleteAsync_PersistsOutcomeCounts()
    {
        var repository = new SyncHistoryRepository(_context);
        var id = await repository.StartAsync("123456789", DateTime.UtcNow.AddMinutes(-2));

        await repository.CompleteAsync(id, DateTime.UtcNow, 42, 180, 3);

        var history = Assert.Single(await repository.GetRecentAsync("123456789"));
        Assert.Equal("completed", history.Status);
        Assert.Equal(42, history.CharacterCount);
        Assert.Equal(180, history.ModCount);
        Assert.Equal(3, history.WarningCount);
        Assert.NotNull(history.CompletedUtc);
    }

    [Fact]
    public async Task FinishAsync_PersistsPrivacySafeFailureOutcome()
    {
        var repository = new SyncHistoryRepository(_context);
        var id = await repository.StartAsync("123456789", DateTime.UtcNow);

        await repository.FinishAsync(
            id,
            DateTime.UtcNow,
            "failed",
            "Account sync could not reach Comlink. Confirm that the local service is running and retry.");

        var history = Assert.Single(await repository.GetRecentAsync("123456789"));
        Assert.Equal("failed", history.Status);
        Assert.Contains("could not reach Comlink", history.ErrorSummary);
    }

    [Fact]
    public async Task StartAsync_PrunesHistoryPerAccountButPreservesOtherAccounts()
    {
        var repository = new SyncHistoryRepository(_context);
        for (var index = 0; index < 22; index++)
        {
            await repository.StartAsync("123456789", DateTime.UtcNow.AddMinutes(index));
        }

        await repository.StartAsync("987654321", DateTime.UtcNow);

        Assert.Equal(20, (await repository.GetRecentAsync("123456789", 50)).Count);
        Assert.Single(await repository.GetRecentAsync("987654321"));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
