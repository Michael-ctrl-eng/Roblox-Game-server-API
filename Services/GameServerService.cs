using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RobloxGameServerAPI.Data;
using RobloxGameServerAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace RobloxGameServerAPI.Services
{
    public class GameServerService : IGameServerService
    {
        private readonly IGameServerRepository _serverRepository;
        private readonly ILogger<GameServerService> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly IPlayerRepository _playerRepository;
        private readonly IPlayerSessionRepository _playerSessionRepository;
        private readonly IServerConfigurationRepository _serverConfigurationRepository;


        public GameServerService(IGameServerRepository serverRepository, ILogger<GameServerService> logger, IMemoryCache memoryCache, IConfiguration configuration, HttpClient httpClient, IPlayerRepository playerRepository, IPlayerSessionRepository playerSessionRepository, IServerConfigurationRepository serverConfigurationRepository)
        {
            _serverRepository = serverRepository;
            _logger = logger;
            _memoryCache = memoryCache;
            _configuration = configuration;
            _httpClient = httpClient;
            _playerRepository = playerRepository;
            _playerSessionRepository = playerSessionRepository;
            _serverConfigurationRepository = serverConfigurationRepository;
        }

        public async Task<ServerResponse> GetServerAsync(Guid serverId)
        {
            string cacheKey = $"server-{serverId}";
            ServerResponse serverResponse;

            if (!_memoryCache.TryGetValue(cacheKey, out serverResponse))
            {
                _logger.LogDebug("Cache miss for server ID: {ServerId}, fetching from database.", serverId); // Example: Debug logging for cache misses
                var server = await _serverRepository.GetServerByIdAsync(serverId);
                if (server == null)
                {
                    _logger.LogWarning("Server not found in database for ID: {ServerId}", serverId); // Example: Warning log for not found resource
                    return null; // Service should return null, controller handles NotFound
                }
                serverResponse = MapServerToResponse(server);

                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(_configuration.GetValue<int>("CacheExpirationMinutes", 5)));

                _memoryCache.Set(cacheKey, serverResponse, cacheEntryOptions);
                _logger.LogDebug("Server ID: {ServerId} cached with expiration of {ExpirationMinutes} minutes.", serverId, _configuration.GetValue<int>("CacheExpirationMinutes", 5)); // Example: Debug logging for cache set
            }
            else
            {
                _logger.LogDebug("Cache hit for server ID: {ServerId}", serverId); // Example: Debug logging for cache hits
            }

            return serverResponse;
        }

        public async Task<IEnumerable<ServerResponse>> GetAllServersAsync()
        {
            _logger.LogInformation("Fetching all game servers from database."); // Example: Information log for data retrieval
            var servers = await _serverRepository.GetAllServersAsync();
            return servers.Select(MapServerToResponse);
        }

        public async Task<ServerResponse> CreateServerAsync(CreateServerRequest createRequest)
        {
            if (string.IsNullOrWhiteSpace(createRequest.Name))
            {
                _logger.LogError("CreateServerAsync - Server name is empty, rejecting request."); // Example: Error log for invalid input
                throw new ArgumentException("Server name cannot be empty.");
            }

            _logger.LogInformation("Creating new game server: {ServerName}, PlaceID: {PlaceID}", createRequest.Name, createRequest.RobloxPlaceID);

            var newServer = new GameServer
            {
                Name = createRequest.Name,
                RobloxPlaceID = createRequest.RobloxPlaceID,
                GameMode = createRequest.GameMode,
                Region = createRequest.Region,
                MaxPlayers = createRequest.MaxPlayers,
                Status = "Starting",
                CreationTimestamp = DateTime.UtcNow,
                LastUpdatedTimestamp = DateTime.UtcNow
            };

            try
            {
                var createdServer = await _serverRepository.CreateServerAsync(newServer);

                // Create default server configuration
                var defaultConfig = new ServerConfiguration { ServerID = createdServer.ServerID }; // Default config
                await _serverConfigurationRepository.CreateConfigurationAsync(defaultConfig);

                return MapServerToResponse(createdServer);
            }
            catch (DbUpdateException ex) // Example: Handling specific database exceptions
            {
                _logger.LogError(ex, "Error creating server in database. Database error details: {DbError}", ex.InnerException?.Message); // Example: Detailed error logging
                throw new ApplicationException("Failed to create server due to database error.", ex); // Re-throw as ApplicationException or more specific custom exception
            }
            catch (Exception ex) // Catch-all for other unexpected errors
            {
                _logger.LogError(ex, "Unexpected error during server creation."); // Example: General error logging
                throw new ApplicationException("An unexpected error occurred while creating the server.", ex); // Re-throw as ApplicationException
            }
        }

        public async Task<ServerResponse> UpdateServerAsync(Guid serverId, UpdateServerRequest updateRequest)
        {
            var existingServer = await _serverRepository.GetServerByIdAsync(serverId);
            if (existingServer == null)
            {
                _logger.LogWarning("UpdateServerAsync - Server not found for ID: {ServerId}, update request rejected.", serverId); // Example: Warning log for update on non-existent resource
                return null; // Service should return null, controller handles NotFound
            }

            if (!string.IsNullOrWhiteSpace(updateRequest.Name)) existingServer.Name = updateRequest.Name;
            if (!string.IsNullOrWhiteSpace(updateRequest.GameMode)) existingServer.GameMode = updateRequest.GameMode;
            if (!string.IsNullOrWhiteSpace(updateRequest.Region)) existingServer.Region = updateRequest.Region;
            if (updateRequest.MaxPlayers.HasValue) existingServer.MaxPlayers = updateRequest.MaxPlayers.Value;
            if (!string.IsNullOrWhiteSpace(updateRequest.Status)) existingServer.Status = updateRequest.Status;

            existingServer.LastUpdatedTimestamp = DateTime.UtcNow;
            try
            {
                var updatedServer = await _serverRepository.UpdateServerAsync(existingServer);
                _memoryCache.Remove($"server-{serverId}"); // Invalidate cache
                _logger.LogInformation("Server ID: {ServerId} updated successfully.", serverId); // Example: Information log for successful update
                return MapServerToResponse(updatedServer);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error updating server in database for ID: {ServerId}. Database error details: {DbError}", serverId, ex.InnerException?.Message); // Example: Detailed error logging for database update
                throw new ApplicationException($"Failed to update server ID: {serverId} due to database error.", ex); // Re-throw as ApplicationException
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during server update for ID: {ServerId}.", serverId, ex); // Example: General error logging for update
                throw new ApplicationException($"An unexpected error occurred while updating server ID: {serverId}.", ex); // Re-throw as ApplicationException
            }
        }

        public async Task<bool> DeleteServerAsync(Guid serverId)
        {
            _logger.LogInformation("Deleting server ID: {ServerId}.", serverId); // Example: Information log for delete operation
            _memoryCache.Remove($"server-{serverId}"); // Invalidate cache
            return await _serverRepository.DeleteServerAsync(serverId);
        }

        public async Task ProcessServerHeartbeatAsync(Guid serverId)
        {
            var server = await _serverRepository.GetServerByIdAsync(serverId);
            if (server == null)
            {
                _logger.LogWarning("ProcessServerHeartbeatAsync - Heartbeat received for non-existent server ID: {ServerId}.", serverId); // Example: Warning log for heartbeat from unknown server
                return; // Heartbeat for non-existent server, just ignore and log
            }

            await _serverRepository.UpdateServerHeartbeatAsync(serverId);
            _logger.LogDebug("Server Heartbeat processed for Server ID: {ServerId}, updated heartbeat timestamp.", serverId); // Example: Debug log for heartbeat processing

            if (server.HeartbeatTimestamp < DateTime.UtcNow.AddMinutes(-5))
            {
                _logger.LogWarning("Server ID: {ServerId} - Heartbeat timeout detected.", serverId); // Example: Warning log for heartbeat timeout
                Console.WriteLine($"Server {serverId} heartbeat timeout detected."); // Keep console output for example, but prefer structured logging in real-world
            }
        }

        public async Task<IEnumerable<ServerResponse>> GetServersByStatusAsync(string status)
        {
            _logger.LogInformation("Fetching game servers by status: {Status}.", status); // Example: Information log for filtered data retrieval
            var servers = await _serverRepository.GetServersByStatusAsync(status);
            return servers.Select(MapServerToResponse);
        }

        public async Task<ServerConfiguration> GetServerConfigurationAsync(Guid serverId)
        {
            _logger.LogDebug("Fetching server configuration for Server ID: {ServerId}.", serverId); // Example: Debug log for config fetch
            return await _serverConfigurationRepository.GetConfigurationByServerIdAsync(serverId);
        }

        public async Task<ServerConfiguration> UpdateServerConfigurationAsync(Guid serverId, ServerConfigurationUpdateRequest updateRequest)
        {
            var existingConfig = await _serverConfigurationRepository.GetConfigurationByServerIdAsync(serverId);
            if (existingConfig == null)
            {
                _logger.LogWarning("UpdateServerConfigurationAsync - Configuration not found for Server ID: {ServerId}, update request rejected.", serverId); // Example: Warning log for config update failure
                return null; // Service should return null, controller handles NotFound
            }

            if (!string.IsNullOrWhiteSpace(updateRequest.MapName)) existingConfig.MapName = updateRequest.MapName;
            if (updateRequest.TimeLimitMinutes.HasValue) existingConfig.TimeLimitMinutes = updateRequest.TimeLimitMinutes.Value;
            if (updateRequest.FriendlyFireEnabled.HasValue) existingConfig.FriendlyFireEnabled = updateRequest.FriendlyFireEnabled.Value;
            if (!string.IsNullOrWhiteSpace(updateRequest.GameRulesJson)) existingConfig.GameRulesJson = updateRequest.GameRulesJson;
            if (!string.IsNullOrWhiteSpace(updateRequest.CustomCommandLineArgs)) existingConfig.CustomCommandLineArgs = updateRequest.CustomCommandLineArgs;
            if (updateRequest.ReservedPorts.HasValue) existingConfig.ReservedPorts = updateRequest.ReservedPorts.Value;
            if (updateRequest.CpuCoresLimit.HasValue) existingConfig.CpuCoresLimit = updateRequest.CpuCoresLimit.Value;
            if (updateRequest.MemoryLimitMB.HasValue) existingConfig.MemoryLimitMB = updateRequest.MemoryLimitMB.Value;

            try
            {
                var updatedConfig = await _serverConfigurationRepository.UpdateConfigurationAsync(existingConfig);
                _logger.LogInformation("Server Configuration updated successfully for Server ID: {ServerId}.", serverId); // Example: Information log for successful config update
                return updatedConfig;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error updating server configuration in database for Server ID: {ServerId}. Database error details: {DbError}", serverId, ex.InnerException?.Message); // Example: Detailed error logging for database config update
                throw new ApplicationException($"Failed to update server configuration for Server ID: {serverId} due to database error.", ex); // Re-throw as ApplicationException
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during server configuration update for Server ID: {ServerId}.", serverId, ex); // Example: General error logging for config update
                throw new ApplicationException($"An unexpected error occurred while updating server configuration for Server ID: {serverId}.", ex); // Re-throw as ApplicationException
            }
        }

        public async Task<ServerHealthInfo> GetServerHealthAsync(Guid serverId)
        {
            var server = await _serverRepository.GetServerByIdAsync(serverId);
            if (server == null)
            {
                _logger.LogWarning("GetServerHealthAsync - Server not found for ID: {ServerId}, health check skipped.", serverId); // Example: Warning log for health check on non-existent server
                return null; // Service should return null, controller handles NotFound
            }

            string healthEndpointUrl = $"http://{server.ServerIP}:{server.ServerPort}/health";

            try
            {
                _logger.LogDebug("Fetching server health for Server ID: {ServerId} from URL: {HealthEndpointUrl}.", serverId, healthEndpointUrl); // Example: Debug log for health check request
                var response = await _httpClient.GetAsync(healthEndpointUrl, HttpCompletionOption.ResponseHeadersRead); // ResponseHeadersRead for performance
                response.EnsureSuccessStatusCode();

                string jsonContent = await response.Content.ReadAsStringAsync();
                var healthInfo = System.Text.Json.JsonSerializer.Deserialize<ServerHealthInfo>(jsonContent);
                _logger.LogDebug("Successfully retrieved server health for Server ID: {ServerId}.", serverId); // Example: Debug log for successful health check

                return healthInfo;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Error fetching server health for ServerID: {ServerId}. URL: {HealthEndpointUrl}. Error: {ErrorMessage}", serverId, healthEndpointUrl, ex.Message); // Example: Warning log for health check failure
                return null; // Health check failed, return null, controller handles gracefully
            }
        }


        public async Task<PlayerSession> PlayerJoinServerAsync(Guid serverId, PlayerJoinRequest request)
        {
            var server = await _serverRepository.GetServerByIdAsync(serverId);
            var player = await _playerRepository.GetPlayerByIdAsync(request.PlayerID);
            if (server == null || player == null)
            {
                _logger.LogWarning("PlayerJoinServerAsync - Server or Player not found. ServerId: {ServerId}, PlayerId: {PlayerId}.", serverId, request.PlayerID); // Example: Warning log for player join failure
                return null; // Service should return null, controller handles NotFound
            }

            var newSession = new PlayerSession
            {
                SessionID = Guid.NewGuid(),
                ServerID = serverId,
                PlayerID = request.PlayerID,
                JoinTime = DateTime.UtcNow,
                PlayerIPAddress = request.PlayerIPAddress
            };

            try
            {
                var createdSession = await _playerSessionRepository.CreateSessionAsync(newSession);
                server.CurrentPlayers++;
                await _serverRepository.UpdateServerAsync(server);
                _logger.LogInformation("Player ID: {PlayerId} joined server ID: {ServerId}, session ID: {SessionId}.", request.PlayerID, serverId, createdSession.SessionID); // Example: Information log for player join
                return createdSession;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error creating player session in database for Server ID: {ServerId}, Player ID: {PlayerId}. Database error details: {DbError}", serverId, request.PlayerID, ex.InnerException?.Message); // Example: Detailed error logging for database player session create
                throw new ApplicationException($"Failed to create player session for Server ID: {serverId}, Player ID: {request.PlayerID} due to database error.", ex); // Re-throw as ApplicationException
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during player join for Server ID: {ServerId}, Player ID: {PlayerId}.", serverId, request.PlayerID, ex); // Example: General error logging for player join
                throw new ApplicationException($"An unexpected error occurred while player joined server ID: {serverId}, Player ID: {request.PlayerID}.", ex); // Re-throw as ApplicationException
            }
        }

        public async Task<bool> PlayerLeaveServerAsync(Guid serverId, PlayerLeaveRequest request)
        {
            var activeSession = await _playerSessionRepository.GetActiveSessionAsync(serverId, request.PlayerID);
            if (activeSession == null)
            {
                _logger.LogWarning("PlayerLeaveServerAsync - Active session not found for Server ID: {ServerId}, Player ID: {PlayerId}.", serverId, request.PlayerID); // Example: Warning log for player leave failure (no active session)
                return false; // Service returns false, controller handles NotFound
            }

            try
            {
                activeSession.LeaveTime = DateTime.UtcNow;
                await _playerSessionRepository.UpdateSessionAsync(activeSession);

                var server = await _serverRepository.GetServerByIdAsync(serverId);
                if (server != null && server.CurrentPlayers > 0)
                {
                    server.CurrentPlayers--;
                    await _serverRepository.UpdateServerAsync(server);
                }
                _logger.LogInformation("Player ID: {PlayerId} left server ID: {ServerId}, session ID: {SessionId}.", request.PlayerID, serverId, activeSession.SessionID); // Example: Information log for player leave
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error updating player session on leave in database for Server ID: {ServerId}, Player ID: {PlayerId}. Database error details: {DbError}", serverId, request.PlayerID, ex.InnerException?.Message); // Example: Detailed error logging for database player session update (leave)
                throw new ApplicationException($"Failed to update player session on leave for Server ID: {serverId}, Player ID: {request.PlayerId} due to database error.", ex); // Re-throw as ApplicationException
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during player leave for Server ID: {ServerId}, Player ID: {PlayerId}.", serverId, request.PlayerID, ex); // Example: General error logging for player leave
                throw new ApplicationException($"An unexpected error occurred while player left server ID: {serverId}, Player ID: {request.PlayerId}.", ex); // Re-throw as ApplicationException
            }
        }

        public async Task<IEnumerable<PlayerSession>> GetActivePlayerSessionsAsync(Guid serverId)
        {
            _logger.LogDebug("Fetching active player sessions for Server ID: {ServerId}.", serverId); // Example: Debug log for fetching active sessions
            return await _playerSessionRepository.GetActiveSessionsByServerIdAsync(serverId);
        }

        public async Task<IEnumerable<ServerResponse>> GetServersForListingAsync(string statusFilter, string gameModeFilter, string regionFilter, string sortBy, string sortOrder)
        {
            _logger.LogInformation("Fetching game servers for listing with filters: Status={Status}, GameMode={GameMode}, Region={Region}, SortBy={SortBy}, SortOrder={SortOrder}.", statusFilter, gameModeFilter, regionFilter, sortBy, sortOrder); // Example: Information log for server listing with filters
            var servers = await _serverRepository.GetAllServersAsync();

            if (!string.IsNullOrEmpty(statusFilter))
            {
                servers = servers.Where(s => s.Status.ToLower() == statusFilter.ToLower());
            }
            if (!string.IsNullOrEmpty(gameModeFilter))
            {
                servers = servers.Where(s => s.GameMode.ToLower() == gameModeFilter.ToLower());
            }
            if (!string.IsNullOrEmpty(regionFilter))
            {
                servers = servers.Where(s => s.Region.ToLower() == regionFilter.ToLower());
            }

            servers = SortServers(servers, sortBy, sortOrder);

            return servers.Select(MapServerToResponse);
        }

        private IEnumerable<GameServer> SortServers(IEnumerable<GameServer> servers, string sortBy, string sortOrder)
        {
            IOrderedEnumerable<GameServer> orderedServers = servers.OrderByDescending(s => s.CurrentPlayers);

            if (!string.IsNullOrEmpty(sortBy))
            {
                sortBy = sortBy.ToLower();
                sortOrder = sortOrder.ToLower();

                switch (sortBy)
                {
                    case "name":
                        orderedServers = (sortOrder == "asc") ? servers.OrderBy(s => s.Name) : servers.OrderByDescending(s => s.Name);
                        break;
                    case "region":
                        orderedServers = (sortOrder == "asc") ? servers.OrderBy(s => s.Region) : servers.OrderByDescending(s => s.Region);
                        break;
                    case "status":
                        orderedServers = (sortOrder == "asc") ? servers.OrderBy(s => s.Status) : servers.OrderByDescending(s => s.Status);
                        break;
                    case "players":
                    default:
                        orderedServers = (sortOrder == "asc") ? servers.OrderBy(s => s.CurrentPlayers) : servers.OrderByDescending(s => s.CurrentPlayers);
                        break;
                }
            }
            return orderedServers;
        }

        private ServerResponse MapServerToResponse(GameServer server)
        {
            return new ServerResponse
            {
                ServerID = server.ServerID,
                Name = server.Name,
                RobloxPlaceID = server.RobloxPlaceID,
                GameMode = server.GameMode,
                Region = server.Region,
                MaxPlayers = server.MaxPlayers,
                CurrentPlayers = server.CurrentPlayers,
                Status = server.Status,
                LastHeartbeat = server.HeartbeatTimestamp
            };
        }
    }
}
