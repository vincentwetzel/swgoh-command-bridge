#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using swgoh_command_bridge.Core.Database.Entities;

namespace swgoh_command_bridge.Core.Database.Repositories
{
    /// <summary>
    /// SQLite implementation of IPlayerRepository utilizing EF Core.
    /// </summary>
    public class PlayerRepository : IPlayerRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PlayerRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlayerRepository"/> class.
        /// </summary>
        public PlayerRepository(AppDbContext context, ILogger<PlayerRepository> logger)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);

            _context = context;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<PlayerEntity?> GetPlayerAsync(string allyCode, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(allyCode);

            _logger.LogInformation("Retrieving cached player data for ally code {AllyCode}", allyCode);

            return await _context.Players
                .AsNoTracking()
                .Include(p => p.Characters)
                .Include(p => p.Mods)
                .FirstOrDefaultAsync(p => p.AllyCode == allyCode, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task SavePlayerAsync(PlayerEntity player, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(player);

            _logger.LogInformation("Saving or updating player cache for ally code {AllyCode}", player.AllyCode);

            IDbContextTransaction? transaction = null;
            if (_context.Database.IsRelational())
            {
                transaction = await _context.Database
                    .BeginTransactionAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            try
            {
                var existingPlayer = await _context.Players
                    .Include(p => p.Characters)
                    .Include(p => p.Mods)
                    .FirstOrDefaultAsync(p => p.AllyCode == player.AllyCode, cancellationToken)
                    .ConfigureAwait(false);

                if (existingPlayer != null)
                {
                    var existingPriorities = existingPlayer.Characters
                        .ToDictionary(character => character.Id, character => character.Priority);

                    existingPlayer.Name = player.Name;
                    existingPlayer.Level = player.Level;
                    existingPlayer.GalacticPower = player.GalacticPower;
                    existingPlayer.LastSyncedUtc = player.LastSyncedUtc;

                    var oldCharacters = existingPlayer.Characters.ToList();
                    _context.Characters.RemoveRange(oldCharacters);
                    existingPlayer.Characters.Clear();
                    foreach (var character in player.Characters)
                    {
                        if (existingPriorities.TryGetValue(character.Id, out var priority))
                        {
                            character.Priority = priority;
                        }

                        character.PlayerAllyCode = existingPlayer.AllyCode;
                        character.Player = existingPlayer;
                        existingPlayer.Characters.Add(character);
                    }

                    var oldMods = existingPlayer.Mods.ToList();
                    _context.Mods.RemoveRange(oldMods);
                    existingPlayer.Mods.Clear();

                    // Flush removals before adding replacement rows with the same composite keys.
                    await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    foreach (var mod in player.Mods)
                    {
                        mod.PlayerAllyCode = existingPlayer.AllyCode;
                        mod.Player = existingPlayer;
                        existingPlayer.Mods.Add(mod);
                    }
                }
                else
                {
                    await _context.Players.AddAsync(player, cancellationToken).ConfigureAwait(false);
                }

                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                if (transaction != null)
                {
                    // Rollback must not be blocked by the caller's cancelled operation.
                    // Otherwise a failed replacement can mask the original exception and
                    // leave recovery dependent on the provider's implicit behavior.
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }

                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeletePlayerAsync(
            string allyCode,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(allyCode);
            var normalizedAllyCode = allyCode.Trim();
            if (normalizedAllyCode.Length == 0)
            {
                throw new ArgumentException("An ally code is required.", nameof(allyCode));
            }

            _logger.LogInformation(
                "Removing cached player data for ally code {AllyCode}",
                normalizedAllyCode);

            IDbContextTransaction? transaction = null;
            if (_context.Database.IsRelational())
            {
                transaction = await _context.Database
                    .BeginTransactionAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            try
            {
                var player = await _context.Players
                    .Include(candidate => candidate.Characters)
                    .Include(candidate => candidate.Mods)
                    .FirstOrDefaultAsync(
                        candidate => candidate.AllyCode == normalizedAllyCode,
                        cancellationToken)
                    .ConfigureAwait(false);
                var syncHistory = await _context.SyncHistory
                    .Where(entry => entry.AllyCode == normalizedAllyCode)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (player == null)
                {
                    if (syncHistory.Count > 0)
                    {
                        _context.SyncHistory.RemoveRange(syncHistory);
                        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    }
                    if (transaction != null)
                    {
                        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    }

                    return syncHistory.Count > 0;
                }

                _context.Characters.RemoveRange(player.Characters);
                _context.Mods.RemoveRange(player.Mods);
                _context.SyncHistory.RemoveRange(syncHistory);
                _context.Players.Remove(player);
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }

                return true;
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }

                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
