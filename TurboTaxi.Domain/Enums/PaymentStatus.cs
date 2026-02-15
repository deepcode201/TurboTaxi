using System;
using System.Collections.Generic;
using System.Text;

namespace TurboTaxi.Domain.Enums
{
    public enum PaymentStatus
    {
        Pending = 0,
        Paid = 1,
        Failed = 2,
        Refunded = 3
    }
}
