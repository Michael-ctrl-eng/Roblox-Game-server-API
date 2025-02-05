// Services/GamePlaceService.cs (Conceptual - Resilience with Retry - Example using Polly)
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RobloxGameServerAPI.Models;
using Polly; // Install Polly NuGet package
using Microsoft.Extensions.Logging; // Import Logger

namespace RobloxGameServerAPI.Services
{
    public class GamePlaceService : IGamePlaceService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GamePlaceService> _logger; // Inject Logger

        public GamePlaceService(HttpClient httpClient, ILogger<GamePlaceService> logger) // Inject Logger
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<RobloxPlaceInfoResponse> GetRobloxPlaceInfoAsync(long robloxPlaceId)
        {
            string apiUrl = $"https://apis.roblox.com/v1/places/{robloxPlaceId}"; // Replace with real Roblox API endpoint

            // --- Resilience Policy - Retry with Polly ---
            var retryPolicy = Policy
                .Handle<HttpRequestException>() // Handle HttpRequestExceptions (e.g., network errors, timeouts)
                .WaitAndRetryAsync(
                    retryCount: 3, // Retry 3 times
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                    onRetry: (exception, timeSpan, retryAttempt, context) =>
                    {
                        _logger.LogWarning(exception, "Retrying Roblox Place Info API request (attempt {RetryAttempt}) after {Delay}s delay. PlaceID={PlaceID}", retryAttempt, timeSpan.Seconds, robloxPlaceId);
                    });

            try
            {
                return await retryPolicy.ExecuteAsync(async () => // Execute the API call with retry policy
                {
                    _logger.LogDebug("Fetching Roblox Place Info from API: PlaceID={PlaceID}, URL={ApiUrl}", robloxPlaceId, apiUrl);
                    var response = await _httpClient.GetAsync(apiUrl);
                    response.EnsureSuccessStatusCode(); // Throw HttpRequestException for non-success status codes

                    string jsonContent = await response.Content.ReadAsStringAsync();
                    var placeInfo = JsonConvert.DeserializeObject<RobloxPlaceInfoResponse>(jsonContent);
                    _logger.LogDebug("Successfully fetched Roblox Place Info from API: PlaceID={PlaceID}", robloxPlaceId);
                    return placeInfo;
                });
            }
            catch (HttpRequestException ex) // Catch HttpRequestException after retries are exhausted
            {
                _logger.LogError(ex, "Error fetching Roblox Place info from API after multiple retries. PlaceID={PlaceID}, URL={ApiUrl}", robloxPlaceId, apiUrl);
                return null; // Or throw a custom ServiceException to indicate external API failure
            }
            catch (Exception ex) // Catch any other unexpected exceptions
            {
                _logger.LogError(ex, "Unexpected error while fetching Roblox Place info. PlaceID={PlaceID}, URL={ApiUrl}", robloxPlaceId, apiUrl);
                return null; // Or throw a custom ServiceException
            }
        }
    }
}
