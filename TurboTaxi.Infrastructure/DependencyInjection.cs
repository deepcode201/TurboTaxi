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
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddHttpClient<IRouteEstimationService, RouteEstimationService>();
            
            // Add memory cache for deduplication
            services.AddMemoryCache();

            services.AddSingleton<IJWTService, JWTService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IUserCrudService, UserCrudService>();
            services.AddScoped<IVehicleCrudService, VehicleCrudService>();
            services.AddScoped<IDriverCrudService, DriverCrudService>();
            services.AddScoped<IRideService, RideService>();
            services.AddScoped<IPendingRideChecker, PendingRideChecker>();
            services.AddScoped<IOffRouteDetectionService, OffRouteDetectionService>();
            services.AddScoped<IRideUserLookupService, RideUserLookupService>();
            
            return services;
        }
    }
}
