using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobloxGameServerAPI.Models;
using RobloxGameServerAPI.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; // Inject Logger

namespace RobloxGameServerAPI.Controllers.v1 // Namespace for API Versioning (v1)
{
    [ApiController]
    [Route("api/v1/servers")] // Route for API Versioning (v1)
    [Authorize] // Requires API Key Authentication by default
    public class ServersController : ControllerBase
    {
        private readonly IGameServerService _serverService;
        private readonly ILogger<ServersController> _logger; // Inject Logger

        public ServersController(IGameServerService serverService, ILogger<ServersController> logger) // Inject Logger
        {
            _serverService = serverService;
            _logger = logger;
        }

        // GET: api/v1/servers
        [HttpGet]
        [Authorize(Policy = "ReadServerStatusPolicy")] // RBAC Policy: Requires "ReadServerStatus" Permission
        [ProducesResponseType(typeof(IEnumerable<ServerResponse>), 200)] // Document successful response type and code
        [ProducesResponseType(401)] // Document Unauthorized response
        [ProducesResponseType(403)] // Document Forbidden response
        [ProducesResponseType(500)] // Document Internal Server Error response
        public async Task<ActionResult<IEnumerable<ServerResponse>>> GetServers()
        {
            _logger.LogDebug("GetServers: Attempting to retrieve all servers.");
            var servers = await _serverService.GetAllServersAsync();
            _logger.LogDebug("GetServers: Retrieved {ServerCount} servers.", servers.Count());
            return Ok(servers); // Return 200 OK with server list
        }

        // GET: api/v1/servers/{serverId}
        [HttpGet("{serverId}")]
        [Authorize(Policy = "ReadServerStatusPolicy")] // RBAC Policy: Requires "ReadServerStatus" Permission
        [ProducesResponseType(typeof(ServerResponse), 200)] // Document successful response type and code
        [ProducesResponseType(404)] // Document Not Found response
        [ProducesResponseType(401)] // Document Unauthorized response
        [ProducesResponseType(403)] // Document Forbidden response
        [ProducesResponseType(500)] // Document Internal Server Error response
        public async Task<ActionResult<ServerResponse>> GetServer(Guid serverId)
        {
            _logger.LogDebug("GetServer: Attempting to retrieve server with ID {ServerId}.", serverId);
            var server = await _serverService.GetServerAsync(serverId);
            if (server == null)
            {
                _logger.LogWarning("GetServer: Server not found for ID {ServerId}.", serverId);
                return NotFound(); // Return 404 Not Found if server doesn't exist
            }
            _logger.LogDebug("GetServer: Retrieved server with ID {ServerId}.", serverId);
            return Ok(server); // Return 200 OK with server details
        }

        // POST: api/v1/servers
        [HttpPost]
        [Authorize(Policy = "ManageServersPolicy")] // RBAC Policy: Requires "ManageServersPolicy" Permission
        [ProducesResponseType(typeof(ServerResponse), 201)] // Document successful creation response type and code
        [ProducesResponseType(400, Type = typeof(ProblemDetails))] // Document Bad Request response with ProblemDetails
        [ProducesResponseType(401)] // Document Unauthorized response
        [ProducesResponseType(403)] // Document Forbidden response
        [ProducesResponseType(500, Type = typeof(ProblemDetails))] // Document Internal Server Error response with ProblemDetails
        public async Task<ActionResult<ServerResponse>> CreateServer([FromBody] CreateServerRequest request)
        {
            _logger.LogInformation("CreateServer: Received request to create a new server: Name={ServerName}, PlaceID={PlaceID}", request.Name, request.RobloxPlaceID);
            try
            {
                var createdServer = await _serverService.CreateServerAsync(request);
                _logger.LogInformation("CreateServer: Server created successfully: ServerID={ServerID}", createdServer.ServerID);
                return CreatedAtAction(nameof(GetServer), new { serverId = createdServer.ServerID }, createdServer); // Return 201 Created with server details and location header
            }
            catch (ArgumentException ex) // Catch ArgumentException for invalid request data
            {
                _logger.LogWarning(ex, "CreateServer: Bad Request - Invalid input data. {ErrorMessage}", ex.Message);
                return BadRequest(Problem(detail: ex.Message, title: "Invalid Server Creation Request", statusCode: 400)); // Return 400 Bad Request with ProblemDetails
            }
            catch (ServiceException ex) // Catch ServiceException for service-layer errors
            {
                _logger.LogError(ex, "CreateServer: Service Error - Failed to create server. {ErrorMessage}", ex.Message);
                return StatusCode(500, Problem(detail: "Failed to create server due to a service error.", title: "Server Creation Error", statusCode: 500)); // Return 500 Internal Server Error with ProblemDetails
            }
            catch (Exception ex) // Catch any other unexpected exceptions
            {
                _logger.LogError(ex, "CreateServer: Internal Server Error - Unexpected error during server creation.", ex);
                return StatusCode(500, Problem(detail: "An unexpected error occurred during server creation.", title: "Internal Server Error", statusCode: 500)); // Return 500 Internal Server Error with ProblemDetails
            }
        }

        // PUT: api/v1/servers/{serverId}
        [HttpPut("{serverId}")]
        [Authorize(Policy = "ManageServersPolicy")] // RBAC Policy: Requires "ManageServersPolicy" Permission
        [ProducesResponseType(typeof(ServerResponse), 200)] // Document successful update response type and code
        [ProducesResponseType(404, Type = typeof(ProblemDetails))] // Document Not Found response with ProblemDetails
        [ProducesResponseType(400, Type = typeof(ProblemDetails))] // Document Bad Request response with ProblemDetails
        [ProducesResponseType(401)] // Document Unauthorized response
        [ProducesResponseType(403)] // Document Forbidden response
        [ProducesResponseType(500, Type = typeof(ProblemDetails))] // Document Internal Server Error response with ProblemDetails
        public async Task<ActionResult<ServerResponse>> UpdateServer(Guid serverId, [FromBody] UpdateServerRequest request)
        {
            _logger.LogInformation("UpdateServer: Received request to update server with ID {ServerId}.", serverId);
            var updatedServer = await _serverService.UpdateServerAsync(serverId, request);
            if (updatedServer == null)
            {
                _logger.LogWarning("UpdateServer: Server not found for ID {ServerId}, update failed.", serverId);
                return NotFound(Problem(detail: $"Server with ID {serverId} not found.", title: "Server Not Found", statusCode: 404)); // Return 404 Not Found with ProblemDetails
            }
            _logger.LogInformation("UpdateServer: Server updated successfully: ServerID={ServerId}", serverId);
            return Ok(updatedServer); // Return 200 OK with updated server details
        }

        // DELETE: api/v1/servers/{serverId}
        [HttpDelete("{serverId}")]
        [Authorize(Policy = "ManageServersPolicy")] // RBAC Policy: Requires "ManageServersPolicy" Permission
        [ProducesResponseType(204)] // Document successful deletion response code (NoContent)
        [ProducesResponseType(404, Type = typeof(ProblemDetails))] // Document Not Found response with ProblemDetails
        [ProducesResponseType(401)] // Document Unauthorized response
        [ProducesResponseType(403)] // Document Forbidden response
        [ProducesResponseType(500, Type = typeof(ProblemDetails))] // Document Internal Server Error response with ProblemDetails
        public async Task<IActionResult> DeleteServer(Guid serverId)
        {
            _logger.LogInformation("DeleteServer: Received request to delete server with ID {ServerId}.", serverId);
            bool deleted = await _serverService.DeleteServerAsync(serverId);
            if (!deleted)
            {
                _logger.LogWarning("DeleteServer: Server not found for ID {ServerId}, deletion failed.", serverId);
                return NotFound(Problem(detail: $"Server with ID {serverId} not found.", title: "Server Not Found", statusCode: 404)); // Return 404 Not Found with ProblemDetails
            }
            _logger.LogInformation("DeleteServer: Server deleted successfully: ServerID={ServerId}", serverId);
            return NoContent(); // Return 204 No Content for successful deletion
        }

        // POST: api/v1/servers/{serverId}/heartbeat
        [HttpPost("{serverId}/heartbeat")]
        [ProducesResponseType(200)] // Document successful heartbeat response code (OK)
        [ProducesResponseType(404, Type = typeof(ProblemDetails))] // Document Not Found response with ProblemDetails
        [ProducesResponseType(500, Type = typeof(ProblemDetails))] // Document Internal Server Error response with ProblemDetails
        public async Task<IActionResult> ServerHeartbeat(Guid serverId)
        {
            _logger.LogDebug("ServerHeartbeat: Received heartbeat ping for ServerID={ServerId}.", serverId);
            try
            {
                await _serverService.ProcessServerHeartbeatAsync(serverId);
                _logger.LogDebug("ServerHeartbeat: Heartbeat processed successfully for ServerID={ServerId}.", serverId);
                return Ok(); // Return 200 OK for successful heartbeat
            }
            catch (ServiceException ex) // Catch ServiceException for service-layer errors during heartbeat processing
            {
                _logger.LogError(ex, "ServerHeartbeat: Service Error - Failed to process heartbeat for ServerID={ServerId}. {ErrorMessage}", serverId, ex.Message);
                return StatusCode(500, Problem(detail: "Failed to process server heartbeat due to a service error.", title: "Server Heartbeat Error", statusCode: 500)); // Return 500 Internal Server Error with ProblemDetails
            }
            catch (Exception ex) // Catch any other unexpected exceptions during heartbeat processing
            {
                _logger.LogError(ex, "ServerHeartbeat: Internal Server Error - Unexpected error processing heartbeat for ServerID={ServerId}.", serverId, ex);
                return StatusCode(500, Problem(detail: "An unexpected error occurred while processing server heartbeat.", title: "Internal Server Error", statusCode: 500)); // Return 500 Internal Server Error with ProblemDetails
            }
        }

        // GET: api/v1/servers/status/{status}
        [HttpGet("status/{status}")]
        [Authorize(Policy = "ReadServerStatusPolicy")] // RBAC Policy: Requires "ReadServerStatus" Permission
        [ProducesResponseType(typeof(IEnumerable<ServerResponse>), 200)] // Document successful response type and code
        [ProducesResponseType(400, Type = typeof(ProblemDetails))] // Document Bad Request response with ProblemDetails
        [ProducesResponseType(401)] // Document Unauthorized response
        [ProducesResponseType(403)] // Document Forbidden response
        [ProducesResponseType(500, Type = typeof(ProblemDetails))] // Document Internal Server Error response with ProblemDetails
        public async Task<ActionResult<IEnumerable<ServerResponse>>> GetServersByStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                _logger.LogWarning("GetServersByStatus: Bad Request - Status parameter is missing or empty.");
                return BadRequest(Problem(detail: "Status parameter is required.", title: "Invalid Request", statusCode: 400)); // Return 400 Bad Request with ProblemDetails
            }
            _logger.LogDebug("GetServersByStatus: Retrieving servers with status '{Status}'.", status);
            var servers = await _serverService.GetServersByStatusAsync(status);
            _logger.LogDebug("GetServersByStatus: Retrieved {ServerCount} servers with status '{Status}'.", servers.Count(), status);
            return Ok(servers); // Return 200 OK with filtered server list
        }

        // GET: api/v1/servers/{serverId}/config
        [HttpGet("{serverId}/config")]
        [Authorize(Policy = "ReadServerStatusPolicy")] // RBAC Policy: Requires "ReadServerStatusPolicy" (or more specific config read policy)
        [ProducesResponseType(typeof(ServerConfiguration), 200)] // Document successful response type and code
        [ProducesResponseType(404, Type = typeof(ProblemDetails))] // Document Not Found response with ProblemDetails
        [ProducesResponseType(401)] // Document Unauthorized response
        [ProducesResponseType(403)] // Document Forbidden response
        [ProducesResponseType(500, Type = typeof(ProblemDetails))] // Document Internal Server Error response with ProblemDetails
        public async Task<ActionResult<ServerConfiguration>> GetServerConfiguration(Guid serverId)
        {
            _logger.LogDebug("GetServerConfiguration: Retrieving configuration for ServerID={ServerId}.", serverId);
            var config = await _serverService.GetServerConfigurationAsync(serverId);
            if (config == null)
            {
                _logger.LogWarning("GetServerConfiguration: Configuration not found for ServerID={ServerId}.", serverId);
                return NotFound(Problem(detail: $"Configuration not found for server with ID {serverId}.", title: "Configuration Not Found", statusCode: 404)); // Return 404 Not Found with ProblemDetails
            }
            _logger.LogDebug("GetServerConfiguration: Retrieved configuration for ServerID={ServerId}.", serverId);
            return Ok(config); // Return 200 OK with server configuration
        }

        // PUT: api/v1/servers/{serverId}/config
        [HttpPut("{serverId}/config")]
        [Authorize(Policy = "UpdateServerConfigPolicy")] // RBAC Policy: Requires "UpdateServerConfigPolicy" Permission
        [ProducesResponseType(typeof(ServerConfiguration), 200)] // Document successful update response type and code
        [ProducesResponseType(404, Type = typeof(ProblemDetails))] // Document Not Found response with ProblemDetails
        [ProducesResponseType(400, Type = typeof(ProblemDetails))] // Document Bad Request response with ProblemDetails
        [ProducesResponseType(401)] // Document Unauthorized response
        [ProducesResponseType(403)] // Document Forbidden response
        [ProducesResponseType(500, Type = typeof(ProblemDetails))] // Document Internal Server Error response with ProblemDetails
        public async Task<ActionResult<ServerConfiguration>> UpdateServerConfiguration(Guid serverId, [FromBody] ServerConfigurationUpdateRequest request)
        {
            if (!ModelState.IsValid) // Check model validation (DataAnnotations)
            {
                _logger.LogWarning("UpdateServerConfiguration: Bad Request - Model validation failed. {ValidationErrors}", ModelState.Values);
                return BadRequest(ModelState); // Return 400 Bad Request with ModelState validation errors
            }
            _logger.LogInformation("UpdateServerConfiguration: Received request to update configuration for ServerID={ServerId}.", serverId);
            var updatedConfig = await _serverService.UpdateServerConfigurationAsync(serverId, request);
            if (updatedConfig == null)
            {
                _logger.LogWarning("UpdateServerConfiguration: Configuration not found for ServerID={ServerId}, update failed.", serverId);
                return NotFound(Problem(detail: $"Configuration not found for server with ID {serverId}.", title: "Configuration Not Found", statusCode: 404)); // Return 404 Not Found with ProblemDetails
            }
            _logger.LogInformation("UpdateServerConfiguration: Configuration updated successfully for ServerID={ServerId}.", serverId);
            return Ok(updatedConfig); // Return 200 OK with updated server configuration
        }

        // GET: api/v1/servers/{serverId}/health
        [HttpGet("{serverId}/health")]
        [Authorize(Policy = "ReadServerStatusPolicy")] // RBAC Policy: Requires "ReadServerStatusPolicy" to read server health
        [ProducesResponseType(typeof(ServerHealthInfo), 200)] // Document successful response type and code
        [ProducesResponseType(404, Type = typeof(ProblemDetails))] // Document Not Found response with ProblemDetails
        [ProducesResponseType(401)] // Document Unauthorized response
        [ProducesResponseType(403)] // Document Forbidden response
        [ProducesResponseType(500, Type = typeof(ProblemDetails))] // Document Internal Server Error response with ProblemDetails
        public async Task<ActionResult<ServerHealthInfo>> GetServerHealth(Guid serverId)
        {
            _logger.LogDebug("GetServerHealth: Retrieving health information for ServerID={ServerId}.", serverId);
            var healthInfo = await _serverService.GetServerHealthAsync(serverId);
            if (healthInfo == null)
            {
                _logger.LogWarning("GetServerHealth: Health information not available for ServerID={ServerId}.", serverId);
                return NotFound(Problem(detail: $"Server health information not available for ServerID: {serverId}.", title: "Server Health Not Available", statusCode: 404)); // Return 404 Not Found with ProblemDetails
            }
            _logger.LogDebug("GetServerHealth: Retrieved health information for ServerID={ServerId}.", serverId);
            return Ok(healthInfo); // Return 200 OK with server health info
        }

        // POST: api/v1/servers/{serverId}/players/join
        [HttpPost("{serverId}/players/join")]
        [ProducesResponseType(200)] // Document successful player join response code (OK)
        [ProducesResponseType(404, Type = typeof(ProblemDetails))] // Document Not Found response with ProblemDetails
        [ProducesResponseType(400, Type = typeof(ProblemDetails))] // Document Bad Request response with ProblemDetails
        [ProducesResponseType(500, Type = typeof(ProblemDetails))] // Document Internal Server Error response with ProblemDetails
        public async Task<ActionResult> PlayerJoinServer(Guid serverId, [FromBody] PlayerJoinRequest request)
        {
            if (!ModelState.IsValid) // Check model validation (DataAnnotations)
            {
                _logger.LogWarning("PlayerJoinServer: Bad Request - Model validation failed. {ValidationErrors}", ModelState.Values);
                return BadRequest(ModelState); // Return 400 Bad Request with ModelState validation errors
            }
            _logger.LogInformation("PlayerJoinServer: Player joining server ServerID={ServerId}, PlayerID={PlayerId}.", serverId, request.PlayerID);
            var session = await _serverService.PlayerJoinServerAsync(serverId, request);
            if (session == null)
            {
                _logger.LogWarning("PlayerJoinServer: Server or Player not found for join request. ServerID={ServerId}, PlayerID={PlayerId}.", serverId, request.PlayerID);
                return NotFound(Problem(detail: "Server or Player not found.", title: "Resource Not Found", statusCode: 404)); // Return 404 Not Found with ProblemDetails
            }
            _logger.LogInformation("PlayerJoinServer: Player joined server successfully. SessionID={SessionId}", session.SessionID);
            return Ok(new { SessionID = session.SessionID }); // Return 200 OK with session ID
        }

        // POST: api/v1/servers/{serverId}/players/leave
        [HttpPost("{serverId}/players/leave")]
        [ProducesResponseType(200)] // Document successful player leave response code (OK)
        [ProducesResponseType(404, Type = typeof(ProblemDetails))] // Document Not Found response with ProblemDetails
        [ProducesResponseType(400, Type = typeof(ProblemDetails))] // Document Bad Request response with ProblemDetails
        [ProducesResponseType(500, Type = typeof(ProblemDetails))] // Document Internal Server Error response with ProblemDetails
        public async Task<ActionResult> PlayerLeaveServer(Guid serverId, [FromBody] PlayerLeaveRequest request)
        {
            if (!ModelState.IsValid) // Check model validation (DataAnnotations)
            {
                _logger.LogWarning("PlayerLeaveServer: Bad Request - Model validation failed. {ValidationErrors}", ModelState.Values);
                return BadRequest(ModelState); // Return 400 Bad Request with ModelState validation errors
            }
            _logger.LogInformation("PlayerLeaveServer: Player leaving server ServerID={ServerId}, PlayerID={PlayerId}.", serverId, request.PlayerID);
            bool success = await _serverService.PlayerLeaveServerAsync(serverId, request);
            if (!success)
            {
                _logger.LogWarning("PlayerLeaveServer: Active session not found for player leave request. ServerID={ServerId}, PlayerID={PlayerId}.", serverId, request.PlayerID);
                return NotFound(Problem(detail: "Active session not found.", title: "Session Not Found", statusCode: 404)); // Return 404 Not Found with ProblemDetails
            }
            _logger.LogInformation("PlayerLeaveServer: Player left server successfully. ServerID={ServerId}, PlayerID={PlayerId}.", serverId, request.PlayerID);
            return Ok(); // Return 200 OK for successful player leave
        }

        // GET: api/v1/servers/{serverId}/players
        [HttpGet("{serverId}/players")]
        [Authorize(Policy = "ReadServerStatusPolicy")] // RBAC Policy: Requires "ReadServerStatusPolicy" to read player lists
        [ProducesResponseType(typeof(IEnumerable<PlayerSession>), 200)] // Document successful response type and code
        [ProducesResponseType(404, Type = typeof(ProblemDetails))] // Document Not Found response with ProblemDetails
        [ProducesResponseType(401)] // Document Unauthorized response
        [ProducesResponseType(403)] // Document Forbidden response
        [ProducesResponseType(500, Type = typeof(ProblemDetails))] // Document Internal Server Error response with ProblemDetails
        public async Task<ActionResult<IEnumerable<PlayerSession>>> GetActivePlayersOnServer(Guid serverId)
        {
            _logger.LogDebug("GetActivePlayersOnServer: Retrieving active players on ServerID={ServerId}.", serverId);
            var sessions = await _serverService.GetActivePlayerSessionsAsync(serverId);
            if (sessions == null)
            {
                _logger.LogWarning("GetActivePlayersOnServer: No active sessions found for ServerID={ServerId}.", serverId);
                return NotFound(Problem(detail: $"No active players found for server with ID {serverId}.", title: "Players Not Found", statusCode: 404)); // Return 404 Not Found with ProblemDetails
            }
            _logger.LogDebug("GetActivePlayersOnServer: Retrieved {PlayerCount} active players for ServerID={ServerId}.", sessions.Count(), serverId);
            return Ok(sessions); // Return 200 OK with list of active player sessions
        }
    }
}
