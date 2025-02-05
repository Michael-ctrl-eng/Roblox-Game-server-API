// Controllers/PlayersController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobloxGameServerAPI.Models;
using RobloxGameServerAPI.Services;
using System;
using System.Threading.Tasks;

namespace RobloxGameServerAPI.Controllers
{
    [ApiController]
    [Route("api/players")]
    [Authorize(Policy = "ServerManagePolicy")] // Example Policy for Player Management
    public class PlayersController : ControllerBase
    {
        private readonly IPlayerService _playerService;

        public PlayersController(IPlayerService playerService)
        {
            _playerService = playerService;
        }

        // GET: api/players/{playerId}
        [HttpGet("{playerId}")]
        public async Task<ActionResult<PlayerResponse>> GetPlayer(Guid playerId)
        {
            var player = await _playerService.GetPlayerAsync(playerId);
            if (player == null)
            {
                return NotFound();
            }
            return Ok(player);
        }

        // POST: api/players
        [HttpPost]
        public async Task<ActionResult<PlayerResponse>> CreatePlayer([FromBody] CreatePlayerRequest request)
        {
            try
            {
                var createdPlayer = await _playerService.CreatePlayerAsync(request);
                return CreatedAtAction(nameof(GetPlayer), new { playerId = createdPlayer.PlayerID }, createdPlayer);
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

        // PUT: api/players/{playerId}
        [HttpPut("{playerId}")]
        public async Task<ActionResult<PlayerResponse>> UpdatePlayer(Guid playerId, [FromBody] UpdatePlayerRequest request)
        {
            var updatedPlayer = await _playerService.UpdatePlayerAsync(playerId, request);
            if (updatedPlayer == null)
            {
                return NotFound();
            }
            return Ok(updatedPlayer);
        }

        // DELETE: api/players/{playerId}
        [HttpDelete("{playerId}")]
        public async Task<IActionResult> DeletePlayer(Guid playerId)
        {
            var deleted = await _playerService.DeletePlayerAsync(playerId);
            if (!deleted)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}
