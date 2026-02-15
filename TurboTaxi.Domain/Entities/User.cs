using System;
using System.Collections.Generic;
using System.Text;
using TurboTaxi.Domain.BaseEntities;

namespace TurboTaxi.Domain.Entities
{
    public class User : BaseEntity
    {
        public string FullName { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;

        public bool IsActive { get; set; } = true;

        // Navigation
        public ICollection<Ride> Rides { get; set; } = new List<Ride>();
        public ICollection<UserPromoCode> UserPromoCodes { get; set; } = new List<UserPromoCode>();
        public ICollection<UserFavoriteLocation> FavoriteLocations { get; set; } = new List<UserFavoriteLocation>();
        public ICollection<Rating> RatingsGiven { get; set; } = new List<Rating>();    // user → driver
        public ICollection<Rating> RatingsReceived { get; set; } = new List<Rating>(); // driver → user (optional)
    }

}
