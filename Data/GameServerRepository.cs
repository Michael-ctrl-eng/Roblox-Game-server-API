using Microsoft.EntityFrameworkCore;
using RobloxGameServerAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RobloxGameServerAPI.Data
{    public class GameServerRepository : IGameServerRepository
    {
        private readonly GameServerDbContext _context;
        private readonly ILogger<GameServerRepository> _logger;

        public GameServerRepository(GameServerDbContext context, ILogger<GameServerRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<GameServer> GetServerByIdAsync(Guid serverId)
        {
            try
            {
                _logger.LogDebug("GetServerByIdAsync: Retrieving server with ID {ServerId} from database.", serverId);
                var server = await _context.GameServers.FindAsync(serverId);
                if (server != null)
                {
                    _logger.LogDebug("GetServerByIdAsync: Server found for ID {ServerId}.", serverId);
                }
                else
                {
                    _logger.LogDebug("GetServerByIdAsync: Server not found for ID {ServerId}.", serverId);
                }
                return server;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetServerByIdAsync: Exception while retrieving server with ID {ServerId}.", serverId);
                throw new DataAccessException("Error retrieving game server data from the database.", ex); // Throw custom DataAccessException
            }
        }

        public async Task<IEnumerable<GameServer>> GetAllServersAsync()
        {
            try
            {
                _logger.LogDebug("GetAllServersAsync: Retrieving all servers from database.");
                var servers = await _context.GameServers.ToListAsync();
                _logger.LogDebug("GetAllServersAsync: Retrieved {ServerCount} servers.", servers.Count);
                return servers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllServersAsync: Exception while retrieving all servers.");
                throw new DataAccessException("Error retrieving game server data from the database.", ex); // Throw custom DataAccessException
            }
        }

        public async Task<GameServer> CreateServerAsync(GameServer server)
        {
        }

        public async Task<GameServer> UpdateServerAsync(GameServer server)
        {
        }

        public async Task<bool> DeleteServerAsync(Guid serverId)
        {
        }

        public async Task UpdateServerHeartbeatAsync(Guid serverId)
        {
            try
            {
                var server = await GetServerByIdAsync(serverId);
                if (server != null)
                {
                    server.HeartbeatTimestamp = DateTime.UtcNow;
                    server.LastUpdatedTimestamp = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    _logger.LogDebug("UpdateServerHeartbeatAsync: Heartbeat updated for ServerID={ServerId}.", serverId);
                }
                else
                {
                    _logger.LogWarning("UpdateServerHeartbeatAsync: Server not found for ID {ServerId}, heartbeat update skipped.", serverId);
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "UpdateServerHeartbeatAsync: Database update exception for ServerID={ServerId}.", serverId);
                throw new DataAccessException("Error updating server heartbeat in the database.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateServerHeartbeatAsync: Unexpected exception for ServerID={ServerId}.", serverId);
                throw new DataAccessException("An unexpected error occurred while updating server heartbeat data.", ex);
            }
        }

        public async Task<IEnumerable<GameServer>> GetServersByStatusAsync(string status)
        {
            try
            {
                _logger.LogDebug("GetServersByStatusAsync: Retrieving servers with status '{Status}' from database.", status);
                var servers = await _context.GameServers
                                             .Where(s => s.Status == status)
                                             .ToListAsync();
                _logger.LogDebug("GetServersByStatusAsync: Retrieved {ServerCount} servers with status '{Status}'.", servers.Count, status);
                return servers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetServersByStatusAsync: Exception while retrieving servers by status '{Status}'.", status, ex);
                throw new DataAccessException("Error retrieving game server data from the database.", ex);
            }
        }
    }
}
