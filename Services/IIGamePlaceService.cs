using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RobloxGameServerAPI.Models;
using Microsoft.Extensions.Configuration;

namespace RobloxGameServerAPI.Services
{
    public class GamePlaceService : IGamePlaceService
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly IConfiguration _configuration; // Inject IConfiguration

        public GamePlaceService(IHttpClientFactory clientFactory, IConfiguration configuration) // Inject configuration
        {
            _clientFactory = clientFactory;
            _configuration = configuration;
        }

        public async Task<RobloxPlaceInfoResponse> GetRobloxPlaceInfoAsync(long robloxPlaceId)
        {
            // Retrieve Roblox API endpoint from configuration (appsettings.json, environment variables, etc.)
            string robloxApiBaseUrl = _configuration.GetValue<string>("RobloxApiBaseUrl", "https://apis.roblox.com/v1"); // Default base URL if not configured
            string apiUrl = $"{robloxApiBaseUrl}/places/{robloxPlaceId}"; // Construct full API URL

            // Create HttpClient using the factory
            var client = _clientFactory.CreateClient();

            try
            {
                var response = await client.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();

                string jsonContent = await response.Content.ReadAsStringAsync();
                var placeInfo = JsonConvert.DeserializeObject<RobloxPlaceInfoResponse>(jsonContent);

                return placeInfo;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error fetching Roblox Place info: {ex.Message}");
                return null;
            }
        }
    }
}
