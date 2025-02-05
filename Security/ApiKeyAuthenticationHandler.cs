using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace RobloxGameServerAPI.Security
{
    public class ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions { }

    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>
    {
        private const string ApiKeyHeaderName = "X-API-Key";
        private readonly ILogger<ApiKeyAuthenticationHandler> _logger;
        private readonly IApiKeyStore _apiKeyStore; // ** NEW:  IApiKeyStore for abstraction **

        public ApiKeyAuthenticationHandler(IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock, ILogger<ApiKeyAuthenticationHandler> apiKeyLogger, IApiKeyStore apiKeyStore) // Inject IApiKeyStore
            : base(options, logger, encoder, clock)
        {
            _logger = apiKeyLogger;
            _apiKeyStore = apiKeyStore; // Assign IApiKeyStore
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey(ApiKeyHeaderName))
            {
                _logger.LogWarning("API Key header missing in request.");
                return AuthenticateResult.Fail("API Key header missing.");
            }

            string apiKey = Request.Headers[ApiKeyHeaderName];

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("API Key missing or empty in request header.");
                return AuthenticateResult.Fail("API Key missing or empty.");
            }

            ApiKeyDetails apiKeyDetails = await _apiKeyStore.GetApiKeyDetailsAsync(apiKey); 

            if (apiKeyDetails == null)
            {
                _logger.LogWarning($"Invalid API Key provided: {apiKey.Substring(0, 4)}... (truncated for security logs)");
                return AuthenticateResult.Fail("Invalid API Key.");
            }

            // API Key is valid - Create claims and principal
            var claims = new List<Claim> {
                new Claim(ClaimTypes.Name, apiKeyDetails.DeveloperName),
                new Claim("ApiKey", apiKey), // Consider NOT adding the API Key itself as a claim in production for better security
                new Claim("ExperienceId", apiKeyDetails.ExperienceId) // Example: Scope claim to ExperienceId
            };

            foreach (var permission in apiKeyDetails.Permissions)
            {
                claims.Add(new Claim("Permission", permission));
            }

            _logger.LogInformation($"API Key Authentication Successful for Developer: {apiKeyDetails.DeveloperName}, ExperienceId: {apiKeyDetails.ExperienceId}, Permissions: {string.Join(", ", apiKeyDetails.Permissions)}");

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
    }

    public class ApiKeyDetails // ** Made public for use in IApiKeyStore interface **
    {
        public string DeveloperName { get; set; }
        public string[] Permissions { get; set; }
        public string ExperienceId { get; set; }
    }

    public interface IApiKeyStore
    {
        Task<ApiKeyDetails> GetApiKeyDetailsAsync(string apiKey);
    }

    public class InMemoryApiKeyStore : IApiKeyStore
    {
        private static readonly Dictionary<string, ApiKeyDetails> ValidApiKeys = new Dictionary<string, ApiKeyDetails>
        {
            { "valid-api-key-1", new ApiKeyDetails { DeveloperName = "Developer 1", Permissions = new[] { "server.manage", "server.status.read" }, ExperienceId = "123456789" } },
            { "valid-api-key-2", new ApiKeyDetails { DeveloperName = "Developer 2", Permissions = new[] { "server.status.read" }, ExperienceId = "987654321" } }
        };

        public async Task<ApiKeyDetails> GetApiKeyDetailsAsync(string apiKey)
        {
            ValidApiKeys.TryGetValue(apiKey, out ApiKeyDetails details); 
            return await Task.FromResult(details);
        }
    }
}
