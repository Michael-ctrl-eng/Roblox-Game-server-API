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
using Serilog; // Import Serilog
using Ganss.XSS; // Import HtmlSanitizer

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
            // --- Infrastructure Services ---
            // 1. Database Context (PostgreSQL) - Scoped Lifecycle
            services.AddDbContext<GameServerDbContext>(options =>
                options.UseNpgsql(Configuration.GetConnectionString("DefaultConnection")),
                ServiceLifetime.Scoped // Explicitly set Scoped lifecycle
            );

            // 2. Redis Cache (Distributed Cache) - Singleton Lifecycle
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = Configuration.GetConnectionString("RedisConnection");
                // options.InstanceName = "RobloxApiCache:"; // Optional
            });

            // 3. HttpClientFactory - Singleton Lifecycle (recommended for HttpClient)
            services.AddHttpClient(); // Registers IHttpClientFactory as Singleton

            // 4. HTML Sanitizer - Singleton Lifecycle (stateless)
            services.AddSingleton<IHtmlSanitizer, HtmlSanitizer>();

            // --- Repositories - Scoped Lifecycle (DbContext is Scoped) ---
            services.AddScoped<IGameServerRepository, GameServerRepository>();
            services.AddScoped<IPlayerRepository, PlayerRepository>();
            services.AddScoped<IPlayerSessionRepository, PlayerSessionRepository>();
            services.AddScoped<IServerConfigurationRepository, ServerConfigurationRepository>();
            services.AddScoped<IGameSettingRepository, GameSettingRepository>();
            services.AddScoped<IApiKeyRepository, ApiKeyRepository>(); // Add ApiKeyRepository

            // --- Services - Scoped or Transient Lifecycle (depending on dependencies) ---
            services.AddScoped<IGameServerService, GameServerService>();
            services.AddScoped<IPlayerService, PlayerService>();
            services.AddScoped<IGamePlaceService, GamePlaceService>();
            services.AddScoped<IApiKeyService, ApiKeyService>(); // Add ApiKeyService

            // --- Validators - Transient Lifecycle (stateless) ---
            services.AddTransient<CreateServerRequestValidator>();

            // --- API Key Authentication Handler - Singleton Lifecycle (if injecting scoped services, consider scoped or transient and factory pattern) ---
            services.AddSingleton<ApiKeyAuthenticationHandler>(); // Singleton if it only depends on Singleton or Transient services

            // --- WebSockets Handler - Singleton Lifecycle (handle connections) ---
            services.AddSingleton<ServerStatusWebSocketHandler>();

            // --- API Controllers with FluentValidation ---
            services.AddControllers()
                    .AddNewtonsoftJson()
                    .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<CreateServerRequestValidator>());

            // --- Swagger/OpenAPI Documentation ---
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Roblox Game Server API", Version = "v1" });
                // Configure security definitions for OAuth 2.0/API Key in Swagger if needed
            });

            // --- Roblox API Key Authentication ---
            services.AddAuthentication("RobloxApiKey")
                .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("RobloxApiKey", options => { });

            // --- Authorization Policies (RBAC) ---
            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder("RobloxApiKey")
                    .RequireAuthenticatedUser()
                    .Build();
                // ... (RBAC Policies from previous example - unchanged)
            });

            // --- Rate Limiting ---
            services.AddMemoryCache();
            services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimiting"));
            services.Configure<IpRateLimitPolicies>(Configuration.GetSection("IpRateLimitPolicies"));
            services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
            services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
            services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
            services.AddHttpContextAccessor();

            // --- Health Checks ---
            services.AddHealthChecks()
                .AddNpgSql(Configuration.GetConnectionString("DefaultConnection"), name: "Database")
                .AddRedis(Configuration.GetConnectionString("RedisConnection"), name: "RedisCache")
                .AddCheck("Self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API is healthy"));

            // --- Configuration Validation at Startup ---
            ValidateConfiguration(); // Call configuration validation method

            Log.Information("Services configured successfully."); // Startup Logging
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Roblox Game Server API v1"));
            }
            else
            {
                app.UseExceptionHandler("/api/error"); // Production error handling endpoint (Controller-based)
                app.UseHsts(); // Enable HSTS in production
            }

            app.UseHttpsRedirection();

            // --- Middleware Pipeline - Order is Important ---
            app.UseMiddleware<CorrelationIdMiddleware>();     // Correlation ID - First
            app.UseRequestLocalization();                   // Localization middleware (if needed)
            app.UseIpRateLimiting();                        // Rate limiting - Before Routing
            app.UseRouting();                               // Routing Middleware

            app.UseRequestLoggingMiddleware();              // Request Logging - After Routing, Before Authentication

            app.UseAuthentication();                         // Authentication - Before Authorization
            app.UseAuthorization();                          // Authorization - After Authentication

            app.UseWebSockets();                             // WebSocket Middleware
            app.MapWebSocketEndpoint<ServerStatusWebSocketHandler>("/ws/serverstatus"); // Map WebSocket handler

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();                 // Map API Controllers
                endpoints.MapHealthChecks("/health");        // Map Health Check Endpoint
                endpoints.MapControllerRoute("error", "api/error", defaults: new { controller = "Error", action = "Error" }); // Error endpoint route
            });

            Log.Information("Middleware pipeline configured."); // Startup Logging
        }

        private void ValidateConfiguration()
        {
            // ... (Configuration Validation from previous example - unchanged, but expanded to validate all critical settings)
        }
    }
}
