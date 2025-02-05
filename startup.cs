// Startup.cs
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
using System;

namespace RobloxGameServerAPI
{    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // 1. Database Context Configuration (PostgreSQL)
            services.AddDbContext<GameServerDbContext>(options =>
                options.UseNpgsql(Configuration.GetConnectionString("DefaultConnection")));

            // 2. Dependency Injection for Repositories and Services
            services.AddScoped<IGameServerRepository, GameServerRepository>();
            services.AddScoped<IGameServerService, GameServerService>();
            services.AddScoped<IPlayerRepository, PlayerRepository>();
            services.AddScoped<IPlayerService, PlayerService>();
            services.AddScoped<IPlayerSessionRepository, PlayerSessionRepository>();
            services.AddScoped<IServerConfigurationRepository, ServerConfigurationRepository>();
            services.AddScoped<IGameSettingRepository, GameSettingRepository>();

            // 3. Register HttpClientFactory and a named HttpClient for GamePlaceService
            services.AddHttpClient<IGamePlaceService, GamePlaceService>(client =>
            {
                // You can configure default headers, timeouts, base address, etc. here if needed
                // Example: client.BaseAddress = new Uri(Configuration.GetValue<string>("RobloxApiBaseUrl", "https://apis.roblox.com/v1"));
            });
            services.AddScoped<IGamePlaceService, GamePlaceService>();


            // 4. API Controllers with FluentValidation
            services.AddControllers()
                    .AddNewtonsoftJson()
                    .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<CreateServerRequestValidator>());

            // 5. Swagger/OpenAPI Documentation
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Roblox Game Server API", Version = "v1" });
                // Configure security definitions for OAuth 2.0/API Key in Swagger if needed
            });

            // 6. Roblox API Key Authentication
            services.AddAuthentication("RobloxApiKey")
                .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("RobloxApiKey", options => { });

            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder("RobloxApiKey")
                    .RequireAuthenticatedUser()
                    .Build();
                options.AddPolicy("ServerManagePolicy", policy =>
                    policy.RequireClaim("Permission", "server.manage"));
                options.AddPolicy("ServerStatusReadPolicy", policy =>
                    policy.RequireClaim("Permission", "server.status.read"));
            });

            // 7. Rate Limiting
            services.AddMemoryCache();
            services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimiting"));
            services.Configure<IpRateLimitPolicies>(Configuration.GetSection("IpRateLimitPolicies"));
            services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
            services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
            services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
            services.AddHttpContextAccessor();

            // 8. Health Checks
            services.AddHealthChecks()
                .AddNpgSql(Configuration.GetConnectionString("DefaultConnection"));

            // 9. Memory Cache
            services.AddMemoryCache();

            // 10. WebSockets
            services.AddSingleton<ServerStatusWebSocketHandler>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Roblox Game Server API v1"));
            }

            app.UseHttpsRedirection();

            // Middleware Ordering is important
            app.UseMiddleware<ExceptionHandlingMiddleware>(); // Global exception handler
            app.UseMiddleware<CorrelationIdMiddleware>();     // Correlation ID for requests
            app.UseIpRateLimiting();                        // Rate limiting middleware

            app.UseRouting();

            app.UseAuthentication();                         // Authentication middleware
            app.UseAuthorization();                          // Authorization middleware

            app.UseWebSockets();                             // WebSocket middleware
            app.MapWebSocketEndpoint<ServerStatusWebSocketHandler>("/ws/serverstatus"); // Map WebSocket handler

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();                 // Map API controllers
                endpoints.MapHealthChecks("/health");        // Map health check endpoint
            });
        }
    }
}
