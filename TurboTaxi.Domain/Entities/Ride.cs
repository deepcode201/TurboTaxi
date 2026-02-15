using System;
using System.Collections.Generic;
using System.Text;
using TurboTaxi.Domain.BaseEntities;
using TurboTaxi.Domain.Enums;

namespace TurboTaxi.Domain.Entities
{
    public class Ride : BaseEntity
    {
        public int UserId { get; set; }
        public int? DriverId { get; set; }          // əvvəl null (Pending), sonradan dolur
        public int StartLocationId { get; set; }
        public int EndLocationId { get; set; }

        // User hansı maşın tipini seçib (Standard / Comfort / Business və s.)
        public VehicleType RequestedVehicleType { get; set; } = VehicleType.Standard;

        // Bu ride hansı Tariff-ə görə hesablanıb
        public int? TariffId { get; set; }

        public RideStatus Status { get; set; } = RideStatus.Pending;

        // Time info
        public DateTime? RequestedTime { get; set; }   // ride request atıldı
        public DateTime? AcceptedTime { get; set; }
        public DateTime? PickUpTime { get; set; }      // OnWay başladığı an
        public DateTime? CompletedTime { get; set; }
        public DateTime? CanceledTime { get; set; }

        // Estimated values (Directions API-dən)
        public double? EstimatedDistanceKm { get; set; }
        public int? EstimatedDurationMinutes { get; set; }
        public decimal? EstimatedFare { get; set; }

        // Actual values
        public double? ActualDistanceKm { get; set; }
        public int? ActualDurationMinutes { get; set; }
        public decimal? ActualFare { get; set; }

        // Polyline (optional – DB-də saxlayırsan, Redis real-time üçün istifadə edir)
        public string? RoutePolyline { get; set; }

        // PromoCode (istifadə oluna bilər)
        public int? PromoCodeId { get; set; }
        public decimal? DiscountAmount { get; set; }

        // Payment
        public int? PaymentId { get; set; }

        // Navigation
        public User User { get; set; } = null!;
        public Driver? Driver { get; set; }
        public Location StartLocation { get; set; } = null!;
        public Location EndLocation { get; set; } = null!;
        public PromoCode? PromoCode { get; set; }
        public Payment? Payment { get; set; }
        public Tariff? Tariff { get; set; }

        public ICollection<RideStatusHistory> StatusHistory { get; set; } = new List<RideStatusHistory>();
        public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    }

}
