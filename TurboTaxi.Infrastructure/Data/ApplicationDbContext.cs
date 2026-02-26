using Microsoft.EntityFrameworkCore;
using TurboTaxi.Domain.Entities;

namespace TurboTaxi.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Driver> Drivers => Set<Driver>();
        public DbSet<DriverStatusHistory> DriverStatusHistories => Set<DriverStatusHistory>();
        public DbSet<Vehicle> Vehicles => Set<Vehicle>();
        public DbSet<Ride> Rides => Set<Ride>();
        public DbSet<RideStatusHistory> RideStatusHistories => Set<RideStatusHistory>();
        public DbSet<Location> Locations => Set<Location>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<PromoCode> PromoCodes => Set<PromoCode>();
        public DbSet<UserPromoCode> UserPromoCodes => Set<UserPromoCode>();
        public DbSet<UserFavoriteLocation> UserFavoriteLocations => Set<UserFavoriteLocation>();
        public DbSet<Rating> Ratings => Set<Rating>();
        public DbSet<Tariff> Tariffs => Set<Tariff>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UserPromoCode>()
                .HasKey(x => new { x.UserId, x.PromoCodeId });

            modelBuilder.Entity<Ride>()
                .HasOne(r => r.Driver)
                .WithMany(d => d.Rides)
                .HasForeignKey(r => r.DriverId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Ride>()
                .HasOne(r => r.User)
                .WithMany(u => u.Rides)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Ride>()
                .HasOne(r => r.Tariff)
                .WithMany()
                .HasForeignKey(r => r.TariffId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Ride>()
                .HasOne(r => r.StartLocation)
                .WithMany(l => l.StartLocationRides)
                .HasForeignKey(r => r.StartLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Ride>()
                .HasOne(r => r.EndLocation)
                .WithMany(l => l.EndLocationRides)
                .HasForeignKey(r => r.EndLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Ride)
                .WithOne(r => r.Payment)
                .HasForeignKey<Payment>(p => p.RideId);

            modelBuilder.Entity<DriverStatusHistory>()
                .HasOne(h => h.Driver)
                .WithMany(d => d.StatusHistory)
                .HasForeignKey(h => h.DriverId);

            modelBuilder.Entity<RideStatusHistory>()
                .HasOne(h => h.Ride)
                .WithMany(r => r.StatusHistory)
                .HasForeignKey(h => h.RideId);

            modelBuilder.Entity<UserFavoriteLocation>()
                .HasOne(uf => uf.User)
                .WithMany(u => u.FavoriteLocations)
                .HasForeignKey(uf => uf.UserId);

            modelBuilder.Entity<Rating>()
                .HasOne(r => r.FromUser)
                .WithMany(u => u.RatingsGiven)
                .HasForeignKey(r => r.FromUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Rating>()
                .HasOne(r => r.ToUser)
                .WithMany(u => u.RatingsReceived)
                .HasForeignKey(r => r.ToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Rating>()
                .HasOne(r => r.ToDriver)
                .WithMany(d => d.RatingsReceived)
                .HasForeignKey(r => r.ToDriverId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Rating>()
                .HasOne(r => r.Ride)
                .WithMany(rid => rid.Ratings)
                .HasForeignKey(r => r.RideId);

            modelBuilder.Entity<Tariff>(entity =>
            {
                entity.Property(x => x.BaseFare).HasColumnType("decimal(18,2)");
                entity.Property(x => x.PricePerKm).HasColumnType("decimal(18,2)");
                entity.Property(x => x.PricePerMinute).HasColumnType("decimal(18,2)");
                entity.Property(x => x.MinimumFare).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<Payment>(entity =>
            {
                entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<PromoCode>(entity =>
            {
                entity.Property(x => x.DiscountValue).HasColumnType("decimal(18,2)");
                entity.Property(x => x.MaxDiscountAmount).HasColumnType("decimal(18,2)");
            });
        }
    }
}
