using System;
using System.Collections.Generic;
using System.Text;
using TurboTaxi.Domain.BaseEntities;

namespace TurboTaxi.Domain.Entities
{
    public class UserFavoriteLocation : BaseEntity
    {
        public int UserId { get; set; }
        public int LocationId { get; set; }

        public string? AliasName { get; set; } // Home, Work və s.

        // Navigation
        public User User { get; set; } = null!;
        public Location Location { get; set; } = null!;
    }

}
