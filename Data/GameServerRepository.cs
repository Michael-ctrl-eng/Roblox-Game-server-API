// Data/GameServerRepository.cs (Enhanced - Error Handling)
using Microsoft.EntityFrameworkCore;
using RobloxGameServerAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; // Import ILogger

namespace RobloxGameServerAPI.Data
{
    public class GameServerRepository : IGameServerRepository
    {
        private readonly GameServerDbContext _context;
        private readonly ILogger<GameServerRepository> _logger; // Inject Logger

        public GameServerRepository(GameServerDbContext context, ILogger<GameServerRepository> logger) // Inject Logger
        {
            _context = context;
            _logger = logger;
        }

        public async Task<GameServer> CreateServerAsync(GameServer server)
        {
            try
            {
                _context.GameServers.Add(server);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Game server created in database: ServerID={ServerID}, Name={ServerName}", server.ServerID, server.Name); // Repository-level logging
                return server;
            }
            catch (DbUpdateException ex) // Catch EF Core database update exceptions
            {
                _logger.LogError(ex, "Database update exception in CreateServerAsync for ServerID={ServerID}, Name={ServerName}.", server.ServerID, server.Name);
                throw new DataAccessException("Error saving game server to the database.", ex); // Re-throw custom DataAccessException
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception in CreateServerAsync for ServerID={ServerID}, Name={ServerName}.", server.ServerID, server.Name);
                throw new DataAccessException("An unexpected error occurred while saving game server data.", ex); // Re-throw custom DataAccessException
            }
        }

        public async Task<GameServer> UpdateServerAsync(GameServer server)
        {
            try
            {
                _context.Entry(server).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                _logger.LogDebug("Game server updated in database: ServerID={ServerID}, Name={ServerName}", server.ServerID, server.Name);
                return server;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database update exception in UpdateServerAsync for ServerID={ServerID}, Name={ServerName}.", server.ServerID, server.Name);
                throw new DataAccessException("Error updating game server in the database.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception in UpdateServerAsync for ServerID={ServerID}, Name={ServerName}.", server.ServerID, server.Name);
                throw new DataAccessException("An unexpected error occurred while updating game server data.", ex);
            }
        }

        public async Task<bool> DeleteServerAsync(Guid serverId)
        {
            try
            {
                var server = await GetServerByIdAsync(serverId);
                if (server == null) return false;
                _context.GameServers.Remove(server);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Game server deleted from database: ServerID={ServerID}", serverId);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database update exception in DeleteServerAsync for ServerID={ServerID}.", serverId);
                throw new DataAccessException("Error deleting game server from the database.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception in DeleteServerAsync for ServerID={ServerID}.", serverId);
                throw new DataAccessException("An unexpected error occurred while deleting game server data.", ex);
            }
        }

        // ... (Other repository methods - GetServerByIdAsync, GetAllServersAsync, UpdateServerHeartbeatAsync, GetServersByStatusAsync - implement similar try-catch blocks and logging)
    }
}
