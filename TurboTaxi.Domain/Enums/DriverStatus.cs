using System;
using System.Collections.Generic;
using System.Text;

namespace TurboTaxi.Domain.Enums
{
    public enum DriverStatus
    {
        Offline = 0,
        Free = 1,
        Busy = 2,
        BusySoon = 3   // sifarişinin bitməyinə ≤ 3 dəqiqə qalıb
    }

}
