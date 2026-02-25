using System;
using System.Collections.Generic;
using System.Text;
using TurboTaxi.Domain.BaseEntities;
using TurboTaxi.Domain.Enums;

namespace TurboTaxi.Domain.Entities
{
    public class Tariff : BaseEntity
    {
        public string Country { get; set; } = "AZ";
        public string City { get; set; } = "Baku";

        public VehicleType VehicleType { get; set; } = VehicleType.Standard;

        // Əsas qiymətləndirmə parametrləri
        public decimal BaseFare { get; set; }           // açılış qiyməti
        public decimal PricePerKm { get; set; }         // km başına
        public decimal PricePerMinute { get; set; }     // dəqiqə başına
        public decimal MinimumFare { get; set; }        // ən az ödəniş

        // Optional - daha professional etmək üçün
        public decimal? FreeKm { get; set; }            // ilk X km pulsuz
        public int? FreeMinutes { get; set; }           // ilk X dəqiqə pulsuz

        // Night / peak çarpanları
        public decimal? NightMultiplier { get; set; }   // məsələn 1.2m
        public TimeSpan? NightStart { get; set; }       // 22:00
        public TimeSpan? NightEnd { get; set; }         // 06:00

        // Ləğv və gözləmə haqları
        public decimal? CancellationFee { get; set; }
        public decimal? WaitingPricePerMinute { get; set; }

        public bool IsActive { get; set; } = true;

        // Tariff-in keçərli olduğu tarix intervalı (optional)
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }

        // Navigation
        public ICollection<Ride> Rides { get; set; } = new List<Ride>();
    }

}
