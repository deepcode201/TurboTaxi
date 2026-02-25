using System;
using System.Collections.Generic;
using System.Text;
using TurboTaxi.Domain.BaseEntities;
using TurboTaxi.Domain.Enums;

namespace TurboTaxi.Domain.Entities
{
    public class Payment : BaseEntity
    {
        public int RideId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "AZN";
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
        public DateTime? PaidTime { get; set; }
        public string? ExternalTransactionId { get; set; } // Stripe/Bank reference
        // Navigation
        public Ride Ride { get; set; } = null!;
    }

}
