// Controllers/ServerListController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobloxGameServerAPI.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RobloxGameServerAPI.Controllers
{
    [ApiController]
    [Route("api/server-list")]
    [Authorize(Policy = "ServerStatusReadPolicy")]
    public class ServerListController : ControllerBase
    {
        private readonly IGameServerService _serverService;

        public ServerListController(IGameServerService serverService)
        {
            _serverService = serverService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServerResponse>>> GetServerList(
            [FromQuery] string status = null,
            [FromQuery] string gameMode = null,
            [FromQuery] string region = null,
            [FromQuery] string sortBy = "players",
            [FromQuery] string sortOrder = "desc"
        )
        {
            var servers = await _serverService.GetServersForListingAsync(status, gameMode, region, sortBy, sortOrder);
            return Ok(servers);
        }
    }
}
