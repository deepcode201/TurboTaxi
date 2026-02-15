using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Realtime.Redis;
using TurboTaxi.Realtime.Services;

namespace TurboTaxi.Realtime
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddRealtime(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var connectionString = configuration.GetConnectionString("Redis") 
                    ?? throw new InvalidOperationException("Redis connection string is required");

                Console.WriteLine("Connecting to Upstash Redis...");
                
                var options = ConfigurationOptions.Parse(connectionString);
                options.AbortOnConnectFail = false;
                options.ConnectRetry = 5;
                options.ConnectTimeout = 10000;
                options.SyncTimeout = 10000;
                options.AsyncTimeout = 10000;
                options.AllowAdmin = false;

                try
                {
                    var multiplexer = ConnectionMultiplexer.Connect(options);
                    Console.WriteLine("Redis connection established successfully");
                    
                    var endpoints = multiplexer.GetEndPoints();
                    Console.WriteLine($"   Connected to: {string.Join(", ", endpoints.Select(e => e.ToString()))}");
                    
                    return multiplexer;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Redis connection failed: {ex.Message}");
                    Console.WriteLine($"   Check your Upstash Redis credentials and connection string");
                    Console.WriteLine($"   Error details: {ex.GetType().Name}");
                    throw new InvalidOperationException("Failed to connect to Redis. Check connection string and network connectivity.", ex);
                }
            });

            services.AddSingleton<IRedisService, RedisService>();
            services.AddScoped<IDriverLocationService, DriverLocationService>();
            services.AddScoped<IRideNotificationService, RideNotificationService>();

            return services;
        }
    }
}
