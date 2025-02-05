// Services/GameServerService.cs
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
        private readonly HttpClient _httpClient; // Direct HttpClient Injection - now using IHttpClientFactory in GamePlaceService
        private readonly IPlayerRepository _playerRepository;
        private readonly IPlayerSessionRepository _playerSessionRepository;
        private readonly IServerConfigurationRepository _serverConfigurationRepository;


        public GameServerService(IGameServerRepository serverRepository, ILogger<GameServerService> logger, IMemoryCache memoryCache, IConfiguration configuration, HttpClient httpClient, IPlayerRepository playerRepository, IPlayerSessionRepository playerSessionRepository, IServerConfigurationRepository serverConfigurationRepository)
        {
            _serverRepository = serverRepository;
            _logger = logger;
            _memoryCache = memoryCache;
            _configuration = configuration;
            _httpClient = httpClient; // Direct HttpClient Injection - now using IHttpClientFactory in GamePlaceService
            _playerRepository = playerRepository;
            _playerSessionRepository = playerSessionRepository;
            _serverConfigurationRepository = serverConfigurationRepository;
        }

        public async Task<ServerResponse> GetServerAsync(Guid serverId)
        {
            string cacheKey = $"server-{serverId}";
            ServerResponse serverResponse;

            // Try to get server response from the cache first
            if (!_memoryCache.TryGetValue(cacheKey, out serverResponse))
            {
                // Cache miss: Fetch server data from the repository
                var server = await _serverRepository.GetServerByIdAsync(serverId);
                if (server == null) return null;
                serverResponse = MapServerToResponse(server);

                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(_configuration.GetValue<int>("CacheExpirationMinutes", 5))); // Sliding expiration: cache entry refreshes if accessed within the expiration period

                // Store the server response in the cache
                _memoryCache.Set(cacheKey, serverResponse, cacheEntryOptions);
            }
            // Cache hit or cache set: return the server response
            return serverResponse;
        }

        public async Task<IEnumerable<ServerResponse>> GetAllServersAsync()
        {
            var servers = await _serverRepository.GetAllServersAsync();
            return servers.Select(MapServerToResponse);
        }

        public async Task<ServerResponse> CreateServerAsync(CreateServerRequest createRequest)
        {
            if (string.IsNullOrWhiteSpace(createRequest.Name))
            {
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

            var createdServer = await _serverRepository.CreateServerAsync(newServer);

            // Create default server configuration
            var defaultConfig = new ServerConfiguration { ServerID = createdServer.ServerID }; // Default config
            await _serverConfigurationRepository.CreateConfigurationAsync(defaultConfig);

            return MapServerToResponse(createdServer);
        }

        public async Task<ServerResponse> UpdateServerAsync(Guid serverId, UpdateServerRequest updateRequest)
        {
            var existingServer = await _serverRepository.GetServerByIdAsync(serverId);
            if (existingServer == null) return null;

            if (!string.IsNullOrWhiteSpace(updateRequest.Name)) existingServer.Name = updateRequest.Name;
            if (!string.IsNullOrWhiteSpace(updateRequest.GameMode)) existingServer.GameMode = updateRequest.GameMode;
            if (!string.IsNullOrWhiteSpace(updateRequest.Region)) existingServer.Region = updateRequest.Region;
            if (updateRequest.MaxPlayers.HasValue) existingServer.MaxPlayers = updateRequest.MaxPlayers.Value;
            if (!string.IsNullOrWhiteSpace(updateRequest.Status)) existingServer.Status = updateRequest.Status;

            existingServer.LastUpdatedTimestamp = DateTime.UtcNow;
            var updatedServer = await _serverRepository.UpdateServerAsync(existingServer);
            _memoryCache.Remove($"server-{serverId}"); // Invalidate cache
            return MapServerToResponse(updatedServer);
        }

        public async Task<bool> DeleteServerAsync(Guid serverId)
        {
            _memoryCache.Remove($"server-{serverId}"); // Invalidate cache
            return await _serverRepository.DeleteServerAsync(serverId);
        }

        public async Task ProcessServerHeartbeatAsync(Guid serverId)
        {
            var server = await _serverRepository.GetServerByIdAsync(serverId);
            if (server == null) return;

            await _serverRepository.UpdateServerHeartbeatAsync(serverId);

            if (server.HeartbeatTimestamp < DateTime.UtcNow.AddMinutes(-5))
            {
                Console.WriteLine($"Server {serverId} heartbeat timeout detected.");
            }
        }

        public async Task<IEnumerable<ServerResponse>> GetServersByStatusAsync(string status)
        {
            var servers = await _serverRepository.GetServersByStatusAsync(status);
            return servers.Select(MapServerToResponse);
        }

        public async Task<ServerConfiguration> GetServerConfigurationAsync(Guid serverId)
        {
            return await _serverConfigurationRepository.GetConfigurationByServerIdAsync(serverId);
        }

        public async Task<ServerConfiguration> UpdateServerConfigurationAsync(Guid serverId, ServerConfigurationUpdateRequest updateRequest)
        {
            var existingConfig = await _serverConfigurationRepository.GetConfigurationByServerIdAsync(serverId);
            if (existingConfig == null) return null;

            if (!string.IsNullOrWhiteSpace(updateRequest.MapName)) existingConfig.MapName = updateRequest.MapName;
            if (updateRequest.TimeLimitMinutes.HasValue) existingConfig.TimeLimitMinutes = updateRequest.TimeLimitMinutes.Value;
            if (updateRequest.FriendlyFireEnabled.HasValue) existingConfig.FriendlyFireEnabled = updateRequest.FriendlyFireEnabled.Value;
            if (!string.IsNullOrWhiteSpace(updateRequest.GameRulesJson)) existingConfig.GameRulesJson = updateRequest.GameRulesJson;
            if (!string.IsNullOrWhiteSpace(updateRequest.CustomCommandLineArgs)) existingConfig.CustomCommandLineArgs = updateRequest.CustomCommandLineArgs;
            if (updateRequest.ReservedPorts.HasValue) existingConfig.ReservedPorts = updateRequest.ReservedPorts.Value;
            if (updateRequest.CpuCoresLimit.HasValue) existingConfig.CpuCoresLimit = updateRequest.CpuCoresLimit.Value;
            if (updateRequest.MemoryLimitMB.HasValue) existingConfig.MemoryLimitMB = updateRequest.MemoryLimitMB.Value;

            var updatedConfig = await _serverConfigurationRepository.UpdateConfigurationAsync(existingConfig);
            return updatedConfig;
        }

        public async Task<ServerHealthInfo> GetServerHealthAsync(Guid serverId)
        {
            var server = await _serverRepository.GetServerByIdAsync(serverId);
            if (server == null) return null;

            string healthEndpointUrl = $"http://{server.ServerIP}:{server.ServerPort}/health";

            try
            {
                var response = await _httpClient.GetAsync(healthEndpointUrl); // Direct HttpClient usage here is acceptable for server health checks, which are less frequent
                response.EnsureSuccessStatusCode();

                string jsonContent = await response.Content.ReadAsStringAsync();
                var healthInfo = System.Text.Json.JsonSerializer.Deserialize<ServerHealthInfo>(jsonContent);

                return healthInfo;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, $"Error fetching server health for ServerID: {serverId}. URL: {healthEndpointUrl}");
                return null;
            }
        }

        public async Task<PlayerSession> PlayerJoinServerAsync(Guid serverId, PlayerJoinRequest request)
        {
            var server = await _serverRepository.GetServerByIdAsync(serverId);
            var player = await _playerRepository.GetPlayerByIdAsync(request.PlayerID);
            if (server == null || player == null) return null;

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
                return createdSession;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating player session.");
                throw;
            }
        }

        public async Task<bool> PlayerLeaveServerAsync(Guid serverId, PlayerLeaveRequest request)
        {
            var activeSession = await _playerSessionRepository.GetActiveSessionAsync(serverId, request.PlayerID);
            if (activeSession == null) return false;

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
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating player session on leave.");
                throw;
            }
        }

        public async Task<IEnumerable<PlayerSession>> GetActivePlayerSessionsAsync(Guid serverId)
        {
            return await _playerSessionRepository.GetActiveSessionsByServerIdAsync(serverId);
        }

        public async Task<IEnumerable<ServerResponse>> GetServersForListingAsync(string statusFilter, string gameModeFilter, string regionFilter, string sortBy, string sortOrder)
        {
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
