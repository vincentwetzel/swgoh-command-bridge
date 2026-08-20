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
        Assert.Equal("Set: Speed; Shape: Arrow", mod.SetSlotSummary);
        Assert.Contains("+10 Speed", mod.SecondaryStatsSummary);

        viewModel.SelectedCharacter = null;

        Assert.Empty(viewModel.CurrentMods);
        Assert.True(viewModel.HasNoCurrentMods);
    }

    [Fact]
    public async Task CharacterPrioritiesViewModel_MovingUnitPersistsTierOrderAndDerivedPriority()
    {
        await SeedPlayersAsync();
        var viewModel = new CharacterPrioritiesViewModel(_context, () => "123456789");
        await viewModel.LoadCharactersAsync();

        var character = Assert.Single(viewModel.Characters);
        await viewModel.MoveCharacterAsync(character, viewModel.RankedTiers[0], 0);

        var persisted = await _context.Characters
            .SingleAsync(item => item.Id == "ACTIVE" && item.PlayerAllyCode == "123456789");
        Assert.Equal(PriorityTier.S, persisted.PriorityTier);
        Assert.Equal(0, persisted.PriorityOrder);
        Assert.Equal(100_000, persisted.Priority);
    }

    [Fact]
    public async Task CharacterPrioritiesViewModel_SwitchingBoardsKeepsShipsSeparate()
    {
        await SeedPlayersAsync();
        _context.Characters.Add(new CharacterEntity
        {
            Id = "XWINGRESISTANCE",
            PlayerAllyCode = "123456789",
            Name = "Resistance X-wing"
        });
        await _context.SaveChangesAsync();

        var viewModel = new CharacterPrioritiesViewModel(_context, () => "123456789");
        await viewModel.LoadCharactersAsync();

        Assert.Single(viewModel.UnrankedTier.Characters);
        Assert.Equal("ACTIVE", viewModel.UnrankedTier.Characters[0].Id);

        viewModel.ShowShips = true;

        Assert.Single(viewModel.UnrankedTier.Characters);
        Assert.Equal("XWINGRESISTANCE", viewModel.UnrankedTier.Characters[0].Id);
        Assert.True(viewModel.HasCharacters);
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
