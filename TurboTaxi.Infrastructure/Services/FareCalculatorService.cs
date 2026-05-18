using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Domain.Enums;
using TurboTaxi.Infrastructure.Data;

namespace TurboTaxi.Infrastructure.Services
{
    /// <summary> 
    /// Calculates ride fares based on distance, duration, vehicle type, and tariffs.
    /// Supports estimated and final fare calculations with optional night surcharges.
    /// </summary>
    public class FareCalculatorService : IFareCalculatorService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<FareCalculatorService> _logger;

        public FareCalculatorService(
            ApplicationDbContext db,
            ILogger<FareCalculatorService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<decimal> CalculateEstimatedFareAsync(
            string vehicleType,
            double? distanceKm,
            int? durationMinutes,
            DateTime? rideStartTime = null,
            string? city = "Baku")
        {
            try
            {
                // Validate inputs
                if (!distanceKm.HasValue || distanceKm <= 0 || !durationMinutes.HasValue || durationMinutes <= 0)
                {
                    _logger.LogWarning($"Invalid distance or duration for fare estimation: distance={distanceKm}km, duration={durationMinutes}min");
                    return 10m; // Default minimum fare
                }

                // Parse vehicle type
                if (!Enum.TryParse<VehicleType>(vehicleType, true, out var parsedVehicleType))
                {
                    _logger.LogWarning($"Invalid vehicle type: {vehicleType}, using Eco as default");
                    parsedVehicleType = VehicleType.Eco;
                }

                // Default city to Baku if not provided
                if (string.IsNullOrWhiteSpace(city))
                    city = "Baku";

                // Get tariff for vehicle type and city
                var tariff = await _db.Tariffs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.VehicleType == parsedVehicleType && t.City == city && t.IsActive);

                // Fallback to any active tariff for this vehicle type if city-specific not found
                if (tariff == null)
                {
                    tariff = await _db.Tariffs
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.VehicleType == parsedVehicleType && t.IsActive);
                }

                if (tariff == null)
                {
                    _logger.LogWarning($"No active tariff found for {parsedVehicleType} in {city}, using default minimum fare");
                    return 10m;
                }

                // Calculate base fare
                decimal fare = tariff.BaseFare;

                // Add distance fare (accounting for free km if applicable)
                double chargeableKm = distanceKm.Value;
                if (tariff.FreeKm.HasValue && chargeableKm > (double)tariff.FreeKm.Value)
                {
                    chargeableKm -= (double)tariff.FreeKm.Value;
                }
                fare += (decimal)(chargeableKm * (double)tariff.PricePerKm);

                // Add time fare (accounting for free minutes if applicable)
                int chargeableMinutes = durationMinutes.Value;
                if (tariff.FreeMinutes.HasValue && chargeableMinutes > tariff.FreeMinutes)
                {
                    chargeableMinutes -= tariff.FreeMinutes.Value;
                }
                fare += (decimal)(chargeableMinutes * (double)tariff.PricePerMinute);

                // Apply night multiplier if applicable
                if (rideStartTime.HasValue && tariff.NightMultiplier.HasValue && IsNightTime(rideStartTime.Value, tariff))
                {
                    fare *= tariff.NightMultiplier.Value;
                }

                // Ensure minimum fare
                fare = Math.Max(fare, tariff.MinimumFare);

                _logger.LogInformation($"Estimated fare calculated: {vehicleType} {distanceKm:F2}km {durationMinutes}min = {fare:F2} AZN");
                return Math.Round(fare, 2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating estimated fare");
                return 10m; // Default minimum fare on error
            }
        }

        public async Task<decimal> CalculateFinalFareAsync(
            string vehicleType,
            double actualDistanceKm,
            int actualDurationMinutes,
            DateTime rideStartTime,
            string? city = "Baku")
        {
            try
            {
                // Validate inputs
                if (actualDistanceKm <= 0 || actualDurationMinutes <= 0)
                {
                    _logger.LogWarning($"Invalid actual distance or duration: distance={actualDistanceKm}km, duration={actualDurationMinutes}min");
                    return 10m;
                }

                // Parse vehicle type
                if (!Enum.TryParse<VehicleType>(vehicleType, true, out var parsedVehicleType))
                {
                    _logger.LogWarning($"Invalid vehicle type: {vehicleType}, using Eco as default");
                    parsedVehicleType = VehicleType.Eco;
                }

                // Default city to Baku if not provided
                if (string.IsNullOrWhiteSpace(city))
                    city = "Baku";

                // Get tariff for vehicle type and city
                var tariff = await _db.Tariffs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.VehicleType == parsedVehicleType && t.City == city && t.IsActive);

                // Fallback to any active tariff for this vehicle type if city-specific not found
                if (tariff == null)
                {
                    tariff = await _db.Tariffs
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.VehicleType == parsedVehicleType && t.IsActive);
                }

                if (tariff == null)
                {
                    _logger.LogWarning($"No active tariff found for {parsedVehicleType} in {city}, using default minimum fare");
                    return 10m;
                }

                // Calculate base fare
                decimal fare = tariff.BaseFare;

                // Add distance fare (accounting for free km if applicable)
                double chargeableKm = actualDistanceKm;
                if (tariff.FreeKm.HasValue && chargeableKm > (double)tariff.FreeKm.Value)
                {
                    chargeableKm -= (double)tariff.FreeKm.Value;
                }
                fare += (decimal)(chargeableKm * (double)tariff.PricePerKm);

                // Add time fare (accounting for free minutes if applicable)
                int chargeableMinutes = actualDurationMinutes;
                if (tariff.FreeMinutes.HasValue && chargeableMinutes > tariff.FreeMinutes)
                {
                    chargeableMinutes -= tariff.FreeMinutes.Value;
                }
                fare += (decimal)(chargeableMinutes * (double)tariff.PricePerMinute);

                // Apply night multiplier if applicable
                if (tariff.NightMultiplier.HasValue && IsNightTime(rideStartTime, tariff))
                {
                    fare *= tariff.NightMultiplier.Value;
                }

                // Ensure minimum fare
                fare = Math.Max(fare, tariff.MinimumFare);

                _logger.LogInformation($"Final fare calculated: {vehicleType} {actualDistanceKm:F2}km {actualDurationMinutes}min = {fare:F2} AZN");
                return Math.Round(fare, 2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating final fare");
                return 10m;
            }
        }

        private bool IsNightTime(DateTime rideTime, Domain.Entities.Tariff tariff)
        {
            if (!tariff.NightStart.HasValue || !tariff.NightEnd.HasValue)
                return false;

            var timeOfDay = rideTime.TimeOfDay;
            var nightStart = tariff.NightStart.Value;
            var nightEnd = tariff.NightEnd.Value;

            // Handle case where night spans midnight (e.g., 22:00 to 06:00)
            if (nightStart > nightEnd)
                return timeOfDay >= nightStart || timeOfDay < nightEnd;
            else
                return timeOfDay >= nightStart && timeOfDay < nightEnd;
        }
    }
}
