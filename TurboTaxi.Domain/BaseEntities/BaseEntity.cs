using System;
using System.Collections.Generic;
using System.Text;

namespace TurboTaxi.Domain.BaseEntities
{
    public abstract class BaseEntity
    {
        public int Id { get; set; }
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedTime { get; set; }
    }

}
