using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Infrastructure.Data;
using TurboTaxi.Infrastructure.Services;

namespace TurboTaxi.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");

                options.UseSqlServer(
                    connectionString,
                    sqlOptions =>
                    {
                        // Add retry policy for transient failures (network timeouts, connection drops)
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(10),
                            errorNumbersToAdd: null
                        );

                        // Command timeout: Allow longer for initial queries
                        sqlOptions.CommandTimeout(60);
                    }
                );
            });

            services.AddHttpClient<IRouteEstimationService, RouteEstimationService>();

            // Add memory cache for deduplication
            services.AddMemoryCache();

            services.AddSingleton<IJWTService, JWTService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IUserCrudService, UserCrudService>();
            services.AddScoped<IVehicleCrudService, VehicleCrudService>();
            services.AddScoped<IDriverCrudService, DriverCrudService>();
            services.AddScoped<IRideService, RideService>();
            services.AddScoped<IPromoCodeService, PromoCodeService>();
            services.AddScoped<IFareCalculatorService, FareCalculatorService>();
            services.AddScoped<IPendingRideChecker, PendingRideChecker>();
            services.AddScoped<IOffRouteDetectionService, OffRouteDetectionService>();
            services.AddScoped<IRideUserLookupService, RideUserLookupService>();
            services.AddScoped<IRideChatAccess, RideChatAccessService>();
            services.AddScoped<IChatService, ChatService>();
            
            return services;
        }
    }
}
