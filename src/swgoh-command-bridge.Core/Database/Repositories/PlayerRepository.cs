#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

            var existingPlayer = await _context.Players
                .Include(p => p.Characters)
                .Include(p => p.Mods)
                .FirstOrDefaultAsync(p => p.AllyCode == player.AllyCode, cancellationToken)
                .ConfigureAwait(false);

            if (existingPlayer != null)
            {
                existingPlayer.Name = player.Name;
                existingPlayer.Level = player.Level;
                existingPlayer.GalacticPower = player.GalacticPower;

                _context.Characters.RemoveRange(existingPlayer.Characters);
                foreach (var character in player.Characters)
                {
                    existingPlayer.Characters.Add(character);
                }

                _context.Mods.RemoveRange(existingPlayer.Mods);
                foreach (var mod in player.Mods)
                {
                    existingPlayer.Mods.Add(mod);
                }
            }
            else
            {
                await _context.Players.AddAsync(player, cancellationToken).ConfigureAwait(false);
            }

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}