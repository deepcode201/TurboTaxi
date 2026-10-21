namespace TurboTaxi.Application.Interfaces
{
    /// <summary>
    /// Service for calculating estimated and final ride fares based on distance, duration, and tariffs.
    /// </summary>
    public interface IFareCalculatorService
    {
        /// <summary>
        /// Calculate estimated fare for a ride based on distance and duration.
        /// </summary>
        /// <param name="vehicleType">Vehicle type (Eco, Comfort, Premium, etc.)</param>
        /// <param name="distanceKm">Distance in kilometers</param>
        /// <param name="durationMinutes">Estimated duration in minutes</param>
        /// <param name="rideStartTime">Optional ride start time for night surcharge calculation</param>
        /// <param name="city">City for tariff lookup (default: Baku)</param>
        /// <returns>Estimated fare amount</returns>
        Task<decimal> CalculateEstimatedFareAsync(
            string vehicleType,
            double? distanceKm,
            int? durationMinutes,
            DateTime? rideStartTime = null,
            string? city = "Baku",
            CancellationToken ct = default);

        /// <summary>
        /// Calculate final fare after ride completion (with actual distance/duration).
        /// </summary>
        /// <param name="vehicleType">Vehicle type</param>
        /// <param name="actualDistanceKm">Actual distance traveled in kilometers</param>
        /// <param name="actualDurationMinutes">Actual ride duration in minutes</param>
        /// <param name="rideStartTime">Ride start time for night surcharge calculation</param>
        /// <param name="city">City for tariff lookup (default: Baku)</param>
        /// <returns>Final fare amount</returns>
        Task<decimal> CalculateFinalFareAsync(
            string vehicleType,
            double actualDistanceKm,
            int actualDurationMinutes,
            DateTime rideStartTime,
            string? city = "Baku");
    }
}
