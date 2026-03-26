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

                Console.WriteLine("Connecting to Redis...");

                var options = ConfigurationOptions.Parse(connectionString);
                options.AbortOnConnectFail = false;
                options.ConnectRetry = 3;
                options.ConnectTimeout = 5000;
                options.SyncTimeout = 5000;
                options.AsyncTimeout = 5000;
                options.AllowAdmin = true;
                options.ReconnectRetryPolicy = new ExponentialRetry(2000);

                // Explicitly set ACL user if provided in connection string (e.g. user=Turbotaxi)
                var userParam = connectionString.Split(',')
                    .Select(p => p.Trim())
                    .FirstOrDefault(p => p.StartsWith("user=", StringComparison.OrdinalIgnoreCase));
                if (userParam != null)
                    options.User = userParam.Substring("user=".Length);

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
                    Console.WriteLine($"   Redis host: {connectionString.Split(',')[0]}");
                    Console.WriteLine($"   Error: {ex.GetType().Name}");
                    throw new InvalidOperationException("Failed to connect to Redis. Check connection string and network connectivity.", ex);
                }
            });

            services.AddSingleton<IRedisService, RedisService>();
            services.AddSingleton<IMessageQueueService, MessageQueueService>();
            services.AddScoped<IDriverLocationService, DriverLocationService>();
            services.AddScoped<IRideNotificationService, RideNotificationService>();

            return services;
        }
    }
}
