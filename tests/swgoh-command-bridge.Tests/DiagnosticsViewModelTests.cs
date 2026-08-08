#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using swgoh_command_bridge.UI.ViewModels;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class DiagnosticsViewModelTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    public DiagnosticsViewModelTests()
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
    public async Task RefreshAsync_ShowsRecentRedactedSyncHistory()
    {
        _context.SyncHistory.AddRange(
            new SyncHistoryEntity
            {
                AllyCode = "123456789",
                StartedUtc = DateTime.UtcNow.AddMinutes(-2),
                CompletedUtc = DateTime.UtcNow.AddMinutes(-1),
                Status = "failed",
                ErrorSummary = "Account sync could not reach Comlink."
            },
            new SyncHistoryEntity
            {
                AllyCode = "987654321",
                StartedUtc = DateTime.UtcNow,
                Status = "running"
            });
        await _context.SaveChangesAsync();

        var viewModel = new DiagnosticsViewModel(
            _context,
            new FakeSettingsService(),
            () => "123456789");

        await viewModel.RefreshAsync();

        Assert.Contains("failed", viewModel.RecentSyncHistoryText);
        Assert.Contains("still in progress", viewModel.RecentSyncHistoryText);
        Assert.Contains("*****6789", viewModel.RecentSyncHistoryText);
        Assert.Contains("*****4321", viewModel.RecentSyncHistoryText);
        Assert.DoesNotContain("123456789", viewModel.RecentSyncHistoryText);
        Assert.DoesNotContain("987654321", viewModel.RecentSyncHistoryText);
    }

    [Fact]
    public async Task RefreshAsync_WhenCacheIsUnavailableExposesRetryableErrorState()
    {
        var viewModel = new DiagnosticsViewModel(
            _context,
            new FakeSettingsService(),
            () => "123456789");
        _context.Dispose();

        await viewModel.RefreshAsync();

        Assert.True(viewModel.HasError);
        Assert.Contains("cache", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cache", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings CurrentSettings { get; private set; } = new();

        public string SettingsPath => "settings.json";

        public string DiagnosticsDirectory => Path.GetTempPath();

        public Task LoadSettingsAsync() => Task.CompletedTask;

        public Task SaveSettingsAsync(AppSettings settings)
        {
            CurrentSettings = settings;
            return Task.CompletedTask;
        }
    }
}
