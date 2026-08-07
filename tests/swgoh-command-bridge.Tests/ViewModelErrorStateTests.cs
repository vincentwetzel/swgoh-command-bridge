#nullable enable

using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Services;
using swgoh_command_bridge.UI.ViewModels;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class ViewModelErrorStateTests
{
    [Fact]
    public async Task Characters_LoadFailureProducesRetryableErrorState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        var context = CreateContext(connection);
        var viewModel = new CharactersViewModel(context, () => "123456789");
        context.Dispose();

        await viewModel.LoadCharactersAsync();
        AssertErrorState(viewModel.HasError, viewModel.ErrorMessage, "characters");

        await viewModel.RefreshCommand.ExecuteAsync(null);
        AssertErrorState(viewModel.HasError, viewModel.ErrorMessage, "characters");
    }

    [Fact]
    public async Task Priorities_LoadFailureProducesRetryableErrorState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        var context = CreateContext(connection);
        var viewModel = new CharacterPrioritiesViewModel(context, () => "123456789");
        context.Dispose();

        await viewModel.LoadCharactersAsync();
        AssertErrorState(viewModel.HasError, viewModel.ErrorMessage, "priorities");

        await viewModel.RefreshCommand.ExecuteAsync(null);
        AssertErrorState(viewModel.HasError, viewModel.ErrorMessage, "priorities");
    }

    [Fact]
    public async Task Mods_LoadFailureProducesRetryableErrorState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        var context = CreateContext(connection);
        var advisor = new ModAdvisorService(
            NullLogger<ModAdvisorService>.Instance,
            new ModMechanicsService());
        var viewModel = new ModsViewModel(
            context,
            advisor,
            thresholdProvider: () => null,
            activeAllyCodeProvider: () => "123456789");
        context.Dispose();

        await viewModel.LoadModsAsync();
        AssertErrorState(viewModel.HasError, viewModel.ErrorMessage, "mods");

        await viewModel.RefreshCommand.ExecuteAsync(null);
        AssertErrorState(viewModel.HasError, viewModel.ErrorMessage, "mods");
    }

    [Fact]
    public async Task Optimizer_LoadFailureProducesRetryableErrorState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        var context = CreateContext(connection);
        var assignment = new ModAssignmentService(
            context,
            NullLogger<ModAssignmentService>.Instance);
        var viewModel = new ModOptimizerViewModel(
            context,
            assignment,
            null,
            activeAllyCodeProvider: () => "123456789");
        context.Dispose();

        await viewModel.LoadCharactersAsync();
        AssertErrorState(viewModel.HasError, viewModel.ErrorMessage, "optimizer");

        await viewModel.RefreshAsync();
        AssertErrorState(viewModel.HasError, viewModel.ErrorMessage, "optimizer");
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }

    private static void AssertErrorState(bool hasError, string message, string screen)
    {
        Assert.True(hasError);
        Assert.False(string.IsNullOrWhiteSpace(message));
        Assert.Contains(screen, message, StringComparison.OrdinalIgnoreCase);
    }
}
