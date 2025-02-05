// Controllers/ServersController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobloxGameServerAPI.Models;
using RobloxGameServerAPI.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RobloxGameServerAPI.Controllers
{
    [ApiController]
    [Route("api/servers")]
    [Authorize] // Default API Key Authentication
    public class ServersController : ControllerBase
    {
        private readonly IGameServerService _serverService;

        public ServersController(IGameServerService serverService)
        {
            _serverService = serverService;
        }

        // GET: api/servers
        [HttpGet]
        [Authorize(Policy = "ServerStatusReadPolicy")]
        public async Task<ActionResult<IEnumerable<ServerResponse>>> GetServers()
        {
            var servers = await _serverService.GetAllServersAsync();
            return Ok(servers);
        }

        // GET: api/servers/{serverId}
        [HttpGet("{serverId}")]
        [Authorize(Policy = "ServerStatusReadPolicy")]
        public async Task<ActionResult<ServerResponse>> GetServer(Guid serverId)
        {
            var server = await _serverService.GetServerAsync(serverId);
            if (server == null)
            {
                return NotFound();
            }
            return Ok(server);
        }

        // POST: api/servers
        [HttpPost]
        [Authorize(Policy = "ServerManagePolicy")]
        public async Task<ActionResult<ServerResponse>> CreateServer([FromBody] CreateServerRequest request)
        {
            try
            {
                var createdServer = await _serverService.CreateServerAsync(request);
                return CreatedAtAction(nameof(GetServer), new { serverId = createdServer.ServerID }, createdServer);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "Internal Server Error");
            }
        }

        // PUT: api/servers/{serverId}
        [HttpPut("{serverId}")]
        [Authorize(Policy = "ServerManagePolicy")]
        public async Task<ActionResult<ServerResponse>> UpdateServer(Guid serverId, [FromBody] UpdateServerRequest request)
        {
            var updatedServer = await _serverService.UpdateServerAsync(serverId, request);
            if (updatedServer == null)
            {
                return NotFound();
            }
            return Ok(updatedServer);
        }

        // DELETE: api/servers/{serverId}
        [HttpDelete("{serverId}")]
        [Authorize(Policy = "ServerManagePolicy")]
        public async Task<IActionResult> DeleteServer(Guid serverId)
        {
            var deleted = await _serverService.DeleteServerAsync(serverId);
            if (!deleted)
            {
                return NotFound();
            }
            return NoContent();
        }

        // POST: api/servers/{serverId}/heartbeat
        [HttpPost("{serverId}/heartbeat")]
        public async Task<IActionResult> ServerHeartbeat(Guid serverId)
        {
            await _serverService.ProcessServerHeartbeatAsync(serverId);
            return Ok();
        }

        // GET: api/servers/status/{status}
        [HttpGet("status/{status}")]
        [Authorize(Policy = "ServerStatusReadPolicy")]
        public async Task<ActionResult<IEnumerable<ServerResponse>>> GetServersByStatus(string status)
        {
            var servers = await _serverService.GetServersByStatusAsync(status);
            return Ok(servers);
        }

        // GET: api/servers/{serverId}/config
        [HttpGet("{serverId}/config")]
        [Authorize(Policy = "ServerStatusReadPolicy")] // Or a more specific config read policy
        public async Task<ActionResult<ServerConfiguration>> GetServerConfiguration(Guid serverId)
        {
            var config = await _serverService.GetServerConfigurationAsync(serverId);
            if (config == null) return NotFound();
            return Ok(config);
        }

        // PUT: api/servers/{serverId}/config
        [HttpPut("{serverId}/config")]
        [Authorize(Policy = "ServerManagePolicy")] // Policy for updating config
        public async Task<ActionResult<ServerConfiguration>> UpdateServerConfiguration(Guid serverId, [FromBody] ServerConfigurationUpdateRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var updatedConfig = await _serverService.UpdateServerConfigurationAsync(serverId, request);
            if (updatedConfig == null) return NotFound();
            return Ok(updatedConfig);
        }

        // GET: api/servers/{serverId}/health
        [HttpGet("{serverId}/health")]
        [Authorize(Policy = "ServerStatusReadPolicy")] // Policy to read server health
        public async Task<ActionResult<ServerHealthInfo>> GetServerHealth(Guid serverId)
        {
            var healthInfo = await _serverService.GetServerHealthAsync(serverId);
            if (healthInfo == null)
            {
                return NotFound($"Server health information not available for ServerID: {serverId}.");
            }
            return Ok(healthInfo);
        }

        // POST: api/servers/{serverId}/players/join
        [HttpPost("{serverId}/players/join")]
        public async Task<ActionResult> PlayerJoinServer(Guid serverId, [FromBody] PlayerJoinRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var session = await _serverService.PlayerJoinServerAsync(serverId, request);
            if (session == null) return NotFound("Server or Player not found.");
            return Ok(new { SessionID = session.SessionID });
        }

        // POST: api/servers/{serverId}/players/leave
        [HttpPost("{serverId}/players/leave")]
        public async Task<ActionResult> PlayerLeaveServer(Guid serverId, [FromBody] PlayerLeaveRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var success = await _serverService.PlayerLeaveServerAsync(serverId, request);
            if (!success) return NotFound("Active session not found.");
            return Ok();
        }

        // GET: api/servers/{serverId}/players
        [HttpGet("{serverId}/players")]
        [Authorize(Policy = "ServerStatusReadPolicy")] // Policy to read player lists
        public async Task<ActionResult<IEnumerable<PlayerSession>>> GetActivePlayersOnServer(Guid serverId)
        {
            var sessions = await _serverService.GetActivePlayerSessionsAsync(serverId);
            return Ok(sessions);
        }
    }
}
