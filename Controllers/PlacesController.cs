// Controllers/PlacesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobloxGameServerAPI.Services;
using System.Threading.Tasks;

namespace RobloxGameServerAPI.Controllers
{
    [ApiController]
    [Route("api/places")]
    [Authorize]
    public class PlacesController : ControllerBase
    {
        private readonly IGamePlaceService _placeService;

        public PlacesController(IGamePlaceService placeService)
        {
            _placeService = placeService;
        }

        [HttpGet("{robloxPlaceId}")]
        [Authorize(Policy = "ServerStatusReadPolicy")]
        public async Task<ActionResult<RobloxPlaceInfoResponse>> GetRobloxPlaceInfo(long robloxPlaceId)
        {
            var placeInfo = await _placeService.GetRobloxPlaceInfoAsync(robloxPlaceId);
            if (placeInfo == null)
            {
                return NotFound($"Roblox Place with ID {robloxPlaceId} not found or error fetching info.");
            }
            return Ok(placeInfo);
        }
    }
}
