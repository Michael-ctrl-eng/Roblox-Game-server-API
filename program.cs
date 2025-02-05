using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace RobloxGameServerAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Configure Serilog for structured logging from the start of the application
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning) // Reduce noise from Microsoft logs
                .Enrich.FromLogContext() // Enrich logs with context properties
                .WriteTo.Console() // Write logs to the console (for development/container logs)
                .WriteTo.File("logs/roblox-api.log", rollingInterval: RollingInterval.Day) // Write logs to daily rolling files
                .CreateLogger();

            try
            {
                Log.Information("Starting web host"); // Log application startup
                CreateHostBuilder(args).Build().Run(); // Build and run the web host
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly"); // Log fatal startup errors
            }
            finally
            {
                Log.CloseAndFlush(); // Ensure all buffered logs are written before application exit
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog() // Integrate Serilog as the logging provider
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>(); // Configure the web host using Startup.cs
                });
    }
}
