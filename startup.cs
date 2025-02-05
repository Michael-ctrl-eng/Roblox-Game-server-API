using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using RobloxGameServerAPI.Data;
using RobloxGameServerAPI.Services;
using RobloxGameServerAPI.Security;
using RobloxGameServerAPI.Middleware;
using FluentValidation.AspNetCore;
using RobloxGameServerAPI.Validators;
using AspNetCoreRateLimit;
using RobloxGameServerAPI.WebSockets;
using Serilog;
using Ganss.XSS;

namespace RobloxGameServerAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // --- Infrastructure Services - Singleton or Scoped Lifecycles ---
            services.AddDbContext<GameServerDbContext>(options => // Scoped DbContext for request lifecycle
                    options.UseNpgsql(Configuration.GetConnectionString("DefaultConnection")),
                    ServiceLifetime.Scoped);

            services.AddStackExchangeRedisCache(options => // Singleton Redis cache client
            {
                options.Configuration = Configuration.GetConnectionString("RedisConnection");
            });

            services.AddHttpClient(); // Singleton HttpClientFactory for efficient HTTP client management
            services.AddSingleton<IHtmlSanitizer, HtmlSanitizer>(); // Singleton HTML Sanitizer for XSS prevention

            // --- Repositories - Scoped Lifecycles (aligned with DbContext) ---
            services.AddScoped<IGameServerRepository, GameServerRepository>();
            services.AddScoped<IPlayerRepository, PlayerRepository>();
            services.AddScoped<IPlayerSessionRepository, PlayerSessionRepository>();
            services.AddScoped<IServerConfigurationRepository, ServerConfigurationRepository>();
            services.AddScoped<IGameSettingRepository, GameSettingRepository>();
            services.AddScoped<IApiKeyRepository, ApiKeyRepository>();

            // --- Services - Scoped Lifecycles (business logic, depend on Repositories) ---
            services.AddScoped<IGameServerService, GameServerService>();
            services.AddScoped<IPlayerService, PlayerService>();
            services.AddScoped<IGamePlaceService, GamePlaceService>();
            services.AddScoped<IApiKeyService, ApiKeyService>();

            // --- Validators - Transient Lifecycles (stateless validation logic) ---
            services.AddTransient<CreateServerRequestValidator>();

            // --- Security - Singleton Lifecycle (Authentication Handler) ---
            services.AddSingleton<ApiKeyAuthenticationHandler>();

            // --- WebSockets - Singleton Lifecycle (WebSocket handler for persistent connections) ---
            services.AddSingleton<ServerStatusWebSocketHandler>();

            // --- API Controllers - Transient Lifecycle (created per request) ---
            services.AddControllers()
                    .AddNewtonsoftJson() // Optional: If you need NewtonsoftJson for specific JSON handling
                    .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<CreateServerRequestValidator>()); // Register FluentValidation validators

            // --- Swagger/OpenAPI Documentation - Singleton Lifecycle ---
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Roblox Game Server API", Version = "v1" });
                // Configure security definitions for OAuth 2.0/API Key in Swagger if needed for documentation
            });

            // --- Roblox API Key Authentication - Authentication Scheme ---
            services.AddAuthentication("RobloxApiKey")
                .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("RobloxApiKey", options => { });

            // --- Authorization Policies (Role-Based Access Control - RBAC) ---
            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder("RobloxApiKey") // Default policy requires API Key authentication
                    .RequireAuthenticatedUser()
                    .Build();
                // Define RBAC policies based on Roles and Permissions (from Models/Authorization)
                options.AddPolicy("DeveloperPolicy", policy => policy.RequireClaim("Role", Models.Authorization.Roles.Developer.ToString()));
                options.AddPolicy("ServerAdminPolicy", policy => policy.RequireClaim("Role", Models.Authorization.Roles.ServerAdmin.ToString()));
                options.AddPolicy("SupportPolicy", policy => policy.RequireClaim("Role", Models.Authorization.Roles.Support.ToString()));
                options.AddPolicy("ManageServersPolicy", policy => policy.RequireClaim("Permission", Models.Authorization.Permissions.CreateServer.ToString(), Models.Authorization.Permissions.UpdateServer.ToString(), Models.Authorization.Permissions.DeleteServer.ToString()));
                options.AddPolicy("ReadServerStatusPolicy", policy => policy.RequireClaim("Permission", Models.Authorization.Permissions.ReadServerStatus.ToString()));
                options.AddPolicy("UpdateServerConfigPolicy", policy => policy.RequireClaim("Permission", Models.Authorization.Permissions.UpdateServerConfig.ToString()));
                // Add more granular policies as needed for different actions and resources
            });

            // --- Rate Limiting - Singleton Lifecycle (Rate Limiting infrastructure) ---
            services.AddMemoryCache(); // Required for AspNetCoreRateLimit
            services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimiting")); // Bind rate limiting options from configuration
            services.Configure<IpRateLimitPolicies>(Configuration.GetSection("IpRateLimitPolicies")); // Bind rate limiting policies from configuration
            services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>(); // Store rate limiting policies in memory cache
            services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>(); // Store rate limit counters in memory cache
            services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>(); // Rate limit configuration provider
            services.AddHttpContextAccessor(); // Required for AspNetCoreRateLimit to access HttpContext

            // --- Health Checks - Singleton Lifecycle ---
            services.AddHealthChecks()
                .AddNpgSql(Configuration.GetConnectionString("DefaultConnection"), name: "Database") // Add health check for PostgreSQL database
                .AddRedis(Configuration.GetConnectionString("RedisConnection"), name: "RedisCache") // Add health check for Redis cache
                .AddCheck("Self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API is healthy")); // Basic self health check for API

            // --- Configuration Validation at Startup - Transient (Validation logic) ---
            ValidateConfiguration(); // Call configuration validation method to ensure required settings are present

            Log.Information("Services configured successfully."); // Startup log message indicating successful service configuration
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage(); // Enable detailed exception page in development
                app.UseSwagger(); // Enable Swagger UI in development
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Roblox Game Server API v1")); // Configure Swagger UI endpoint
            }
            else
            {
                app.UseExceptionHandler("/api/error"); // Production error handling: Use ErrorController for user-friendly error pages
                app.UseHsts(); // Enable HSTS (HTTP Strict Transport Security) in production for enhanced security
            }

            app.UseHttpsRedirection(); // Redirect HTTP requests to HTTPS in production

            // --- Middleware Pipeline - Order is Crucial for Request Processing ---
            app.UseMiddleware<CorrelationIdMiddleware>();     // 1. Correlation ID Middleware - First to generate/propagate correlation IDs
            app.UseRequestLocalization();                   // 2. Request Localization Middleware - For handling localized content (if needed)
            app.UseIpRateLimiting();                        // 3. Rate Limiting Middleware - Protect API from abuse, before routing
            app.UseRouting();                               // 4. Routing Middleware - Maps requests to controllers/endpoints

            app.UseRequestLoggingMiddleware();              // 5. Request Logging Middleware - Log requests AFTER routing, BEFORE authentication

            app.UseAuthentication();                         // 6. Authentication Middleware - Authenticate users/clients based on credentials
            app.UseAuthorization();                          // 7. Authorization Middleware - Authorize access based on roles/policies (RBAC)

            app.UseWebSockets();                             // 8. WebSocket Middleware - Enable WebSocket support
            app.MapWebSocketEndpoint<ServerStatusWebSocketHandler>("/ws/serverstatus"); // 9. Map WebSocket Handler to /ws/serverstatus endpoint

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();                 // 10. Map API Controllers - Maps controller actions to endpoints
                endpoints.MapHealthChecks("/health");        // 11. Map Health Check Endpoint - Expose health check endpoint
                endpoints.MapControllerRoute("error", "api/error", defaults: new { controller = "Error", action = "Error" }); // 12. Map Error Endpoint - For production error handling via ErrorController
            });

            Log.Information("Middleware pipeline configured."); // Startup log message indicating successful middleware configuration
        }

        private void ValidateConfiguration()
        {
            // --- Configuration Validation at Startup ---
            var connectionString = Configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Database connection string 'DefaultConnection' is not configured in appsettings.json or environment variables.");
            }

            var redisConnectionString = Configuration.GetConnectionString("RedisConnection");
            if (string.IsNullOrEmpty(redisConnectionString))
            {
                throw new InvalidOperationException("Redis connection string 'RedisConnection' is not configured in appsettings.json or environment variables.");
            }

            var rateLimitOptions = Configuration.GetSection("IpRateLimiting").Get<IpRateLimitOptions>();
            if (rateLimitOptions == null)
            {
                throw new InvalidOperationException("Rate limiting configuration 'IpRateLimiting' section is missing in appsettings.json.");
            }
            // Add more configuration validations here for critical settings
            Log.Information("Configuration validation successful."); // Log success if all validations pass
        }
    }
}
