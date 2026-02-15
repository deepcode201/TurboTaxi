using System;
using System.Collections.Generic;
using System.Text;
using TurboTaxi.Domain.BaseEntities;

namespace TurboTaxi.Domain.Entities
{
    public class Location : BaseEntity
    {
        public string? Name { get; set; }            // “Bakı Dövlət Universiteti” kimi
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // Navigation
        public ICollection<Ride> StartLocationRides { get; set; } = new List<Ride>();
        public ICollection<Ride> EndLocationRides { get; set; } = new List<Ride>();
    }

}
