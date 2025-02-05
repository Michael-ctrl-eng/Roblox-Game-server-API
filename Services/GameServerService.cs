using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RobloxGameServerAPI.Data;
using RobloxGameServerAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using RobloxGameServerAPI.Validators; // Import Validators
using Microsoft.EntityFrameworkCore.Storage; // Import for transactions
using RobloxGameServerAPI.Data; // Import DataAccessException
using RobloxGameServerAPI.Services; // Import ServiceException
using Ganss.XSS; // Import HTML Sanitizer

namespace RobloxGameServerAPI.Services
{
    public class GameServerService : IGameServerService
    {
        private readonly IGameServerRepository _serverRepository;
        private readonly ILogger<GameServerService> _logger;
        private readonly IDistributedCache _distributedCache; // Use IDistributedCache for Redis
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly IPlayerRepository _playerRepository;
        private readonly IPlayerSessionRepository _playerSessionRepository;
        private readonly IServerConfigurationRepository _serverConfigurationRepository;
        private readonly GameServerDbContext _context; // Inject DbContext for transactions
        private readonly CreateServerRequestValidator _createServerRequestValidator; // Inject Validator
        private readonly IHtmlSanitizer _htmlSanitizer; // Inject HTML Sanitizer

        public GameServerService(/* ... DI parameters - expanded to include DbContext, Validator, Sanitizer */)
        {
            // ... Constructor - expanded DI parameters and assignments
        }

        public async Task<ServerResponse> GetServerAsync(Guid serverId)
        {
            string cacheKey = $"server-{serverId}";

            // 1. Try to get from Distributed Cache (Redis)
            string cachedServerResponseJson = await _distributedCache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedServerResponseJson))
            {
                _logger.LogDebug("Retrieved server from distributed cache (Redis): ServerID={ServerID}", serverId);
                return JsonSerializer.Deserialize<ServerResponse>(cachedServerResponseJson);
            }

            // 2. If not in cache, fetch from database
            var server = await _serverRepository.GetServerByIdAsync(serverId);
            if (server == null)
            {
                _logger.LogWarning("Server not found in database: ServerID={ServerID}", serverId);
                return null; // Or throw NotFoundException
            }
            var serverResponse = MapServerToResponse(server);

            // 3. Cache in Distributed Cache (Redis) for future requests
            var cacheEntryOptions = new DistributedCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(_configuration.GetValue<int>("CacheExpirationMinutes", 5)));
            await _distributedCache.SetStringAsync(cacheKey, JsonSerializer.Serialize(serverResponse), cacheEntryOptions);
            _logger.LogDebug("Cached server in distributed cache (Redis): ServerID={ServerID}", serverId);

            return serverResponse;
        }

        public async Task<ServerResponse> CreateServerAsync(CreateServerRequest createRequest)
        {
            // 1. Service-level validation using FluentValidation
            var validationResult = _createServerRequestValidator.Validate(createRequest);
            if (!validationResult.IsValid)
            {
                var errorMessages = string.Join(", ", validationResult.Errors.Select(error => error.ErrorMessage));
                _logger.LogWarning("Invalid CreateServerRequest: {Errors}", errorMessages);
                throw new ArgumentException($"Invalid CreateServerRequest: {errorMessages}"); // Or custom ValidationException
            }

            // 2. Data Sanitization (Example - Server Name)
            string sanitizedServerName = _htmlSanitizer.Sanitize(createRequest.Name);
            if (sanitizedServerName != createRequest.Name)
            {
                _logger.LogWarning("Server name sanitized for XSS prevention: Original='{OriginalName}', Sanitized='{SanitizedName}'", createRequest.Name, sanitizedServerName);
            }

            var newServer = new GameServer
            {
                Name = sanitizedServerName, // Use sanitized name
                RobloxPlaceID = createRequest.RobloxPlaceID,
                GameMode = createRequest.GameMode,
                Region = createRequest.Region,
                MaxPlayers = createRequest.MaxPlayers,
                Status = "Starting",
                CreationTimestamp = DateTime.UtcNow,
                LastUpdatedTimestamp = DateTime.UtcNow
            };

            using (IDbContextTransaction transaction = _context.Database.BeginTransaction()) // Transaction for atomicity
            {
                try
                {
                    var createdServer = await _serverRepository.CreateServerAsync(newServer);
                    var defaultConfig = new ServerConfiguration { ServerID = createdServer.ServerID };
                    await _serverConfigurationRepository.CreateConfigurationAsync(defaultConfig);

                    transaction.Commit();
                    _logger.LogInformation("Game server created successfully: ServerID={ServerID}, Name={ServerName}", createdServer.ServerID, createdServer.Name);
                    return MapServerToResponse(createdServer);
                }
                catch (DataAccessException ex) // Catch DataAccessException from Repository
                {
                    transaction.Rollback();
                    _logger.LogError(ex, "Data access error during server creation transaction. Transaction rolled back.");
                    throw new ServiceException("Failed to create server due to a database error.", ex); // Re-throw ServiceException
                }
                catch (Exception ex) // Catch any other unexpected exceptions
                {
                    transaction.Rollback();
                    _logger.LogError(ex, "Unexpected error during server creation transaction. Transaction rolled back.");
                    throw new ServiceException("An unexpected error occurred while creating the server.", ex);
                }
            }
        }

        // ... (Other service methods - UpdateServerAsync, DeleteServerAsync, ProcessServerHeartbeatAsync, etc. - implement similar error handling, caching invalidation, and transaction management where needed)

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
