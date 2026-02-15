using System;
using System.Collections.Generic;
using System.Text;

namespace TurboTaxi.Domain.Enums
{
    public enum RideStatus
    {
        Pending = 0,   // request atılıb, sürücü hələ accept etməyib
        Approved = 1,  // sürücü accept edib
        OnWay = 2,     // sürücü gedir / userlə birlikdə gedirlər
        Canceled = 3,
        Completed = 4
    }

}
