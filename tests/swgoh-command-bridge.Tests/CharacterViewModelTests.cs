#nullable enable

using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.UI.ViewModels;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class CharacterViewModelTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    public CharacterViewModelTests()
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
    public async Task CharactersViewModel_LoadsOnlyTheActiveAccountScope()
    {
        await SeedPlayersAsync();
        var viewModel = new CharactersViewModel(_context, () => "123456789");

        await viewModel.LoadCharactersAsync();

        var character = Assert.Single(viewModel.Characters);
        Assert.Equal("ACTIVE", character.Id);
        Assert.Equal(OperationStatus.Success, viewModel.State.Status);
    }

    [Fact]
    public async Task CharactersViewModel_WithEmptyActiveScopeDoesNotShowOtherAccounts()
    {
        await SeedPlayersAsync();
        var viewModel = new CharactersViewModel(_context, () => string.Empty);

        await viewModel.LoadCharactersAsync();

        Assert.Empty(viewModel.Characters);
        Assert.Equal(OperationStatus.Empty, viewModel.State.Status);
    }

    [Fact]
    public async Task CharactersViewModel_SelectsCharacterAndShowsItsEquippedMods()
    {
        await SeedPlayersAsync();
        _context.Mods.AddRange(
            new GameModEntity
            {
                Id = "ACTIVE-MOD",
                PlayerAllyCode = "123456789",
                CharacterId = "ACTIVE",
                Set = 4,
                Slot = 2,
                Level = 15,
                Tier = 5,
                Rarity = 6,
                PrimaryStatType = "Speed",
                PrimaryStatValue = 30,
                SecondaryStatsJson = "[{\"Type\":\"Speed\",\"Value\":10,\"RollCount\":1}]"
            },
            new GameModEntity
            {
                Id = "OTHER-MOD",
                PlayerAllyCode = "987654321",
                CharacterId = "OTHER",
                Set = 1,
                Slot = 1,
                Level = 15,
                Tier = 5,
                Rarity = 6
            });
        await _context.SaveChangesAsync();

        var viewModel = new CharactersViewModel(_context, () => "123456789");

        await viewModel.LoadCharactersAsync();

        var character = Assert.Single(viewModel.Characters);
        Assert.Same(character, viewModel.SelectedCharacter);
        var mod = Assert.Single(viewModel.CurrentMods);
        Assert.Equal("ACTIVE-MOD", mod.Id);
        Assert.Equal("Speed • Arrow", mod.SetSlotSummary);
        Assert.Contains("+10 Speed", mod.SecondaryStatsSummary);

        viewModel.SelectedCharacter = null;

        Assert.Empty(viewModel.CurrentMods);
        Assert.True(viewModel.HasNoCurrentMods);
    }

    [Fact]
    public async Task CharacterPrioritiesViewModel_ValidatesAndPersistsPriorityChanges()
    {
        await SeedPlayersAsync();
        var viewModel = new CharacterPrioritiesViewModel(_context, () => "123456789");
        await viewModel.LoadCharactersAsync();

        var character = Assert.Single(viewModel.Characters);
        viewModel.SelectedCharacter = character;
        viewModel.SelectedCharacterPriority = 101;
        await viewModel.SavePriorityCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasValidationError);
        Assert.Contains("between 0 and 100", viewModel.ValidationError);

        viewModel.SelectedCharacterPriority = 80;
        await viewModel.SavePriorityCommand.ExecuteAsync(null);

        var persisted = await _context.Characters
            .SingleAsync(item => item.Id == "ACTIVE" && item.PlayerAllyCode == "123456789");
        Assert.Equal(80, persisted.Priority);
        Assert.False(viewModel.HasValidationError);
    }

    [Fact]
    public async Task CharacterPrioritiesViewModel_CancelRestoresDirtyEditAndRefreshPreservesSelection()
    {
        await SeedPlayersAsync();
        var viewModel = new CharacterPrioritiesViewModel(_context, () => "123456789");
        await viewModel.LoadCharactersAsync();

        var selected = Assert.Single(viewModel.Characters);
        viewModel.SelectedCharacter = selected;
        viewModel.SelectedCharacterPriority = 75;

        Assert.True(viewModel.IsDirty);
        viewModel.CancelEditCommand.Execute(null);

        Assert.Equal(10, viewModel.SelectedCharacterPriority);
        Assert.False(viewModel.IsDirty);

        viewModel.SelectedCharacterPriority = 75;
        await viewModel.SavePriorityCommand.ExecuteAsync(null);
        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("ACTIVE", viewModel.SelectedCharacter!.Id);
        Assert.Equal(75, viewModel.SelectedCharacterPriority);
        Assert.Equal(OperationStatus.Success, viewModel.State.Status);
    }

    [Fact]
    public async Task CharacterPrioritiesViewModel_EmptyActiveScopeReportsEmptyState()
    {
        await SeedPlayersAsync();
        var viewModel = new CharacterPrioritiesViewModel(_context, () => string.Empty);

        await viewModel.LoadCharactersAsync();

        Assert.Empty(viewModel.Characters);
        Assert.Equal(OperationStatus.Empty, viewModel.State.Status);
        Assert.True(viewModel.IsEmpty);
        Assert.False(viewModel.HasCharacters);
    }

    private async Task SeedPlayersAsync()
    {
        _context.Players.Add(new PlayerEntity
        {
            AllyCode = "123456789",
            Name = "Active",
            Characters =
            {
                new CharacterEntity
                {
                    Id = "ACTIVE",
                    PlayerAllyCode = "123456789",
                    Name = "Active Character",
                    Priority = 10
                }
            }
        });
        _context.Players.Add(new PlayerEntity
        {
            AllyCode = "987654321",
            Name = "Other",
            Characters =
            {
                new CharacterEntity
                {
                    Id = "OTHER",
                    PlayerAllyCode = "987654321",
                    Name = "Other Character",
                    Priority = 90
                }
            }
        });
        await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
