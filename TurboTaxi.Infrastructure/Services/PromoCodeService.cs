using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Domain.Entities;
using TurboTaxi.Domain.Enums;
using TurboTaxi.Infrastructure.Data;
using TurboTaxi.Models.PromoCodes;

namespace TurboTaxi.Infrastructure.Services
{
    public class PromoCodeService : IPromoCodeService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<PromoCodeService> _logger;

        public PromoCodeService(ApplicationDbContext db, ILogger<PromoCodeService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<CreatePromoCodeResponse> CreateAsync(CreatePromoCodeRequest request, CancellationToken ct = default)
        {
            if (request.Number <= 0)
                throw new InvalidOperationException("Discount number must be greater than 0.");

            if (request.ExpireRideCount <= 0)
                throw new InvalidOperationException("ExpireRideCount must be greater than 0.");

            if (request.IsPercentage == request.IsAmount)
                throw new InvalidOperationException("Specify either IsPercentage or IsAmount.");

            var userExists = await _db.Users.AnyAsync(u => u.Id == request.UserId, ct);
            if (!userExists)
                throw new KeyNotFoundException($"User #{request.UserId} not found");

            var discountType = request.IsPercentage ? PromoDiscountType.Percentage : PromoDiscountType.FixedAmount;

            var promoCode = new PromoCode
            {
                Code = $"PROMO-{Guid.NewGuid():N}".ToUpperInvariant(),
                DiscountType = discountType,
                DiscountValue = request.Number,
                MaxUsagePerUser = request.ExpireRideCount,
                IsActive = true
            };

            _db.PromoCodes.Add(promoCode);
            await _db.SaveChangesAsync(ct);

            var userPromoCode = new UserPromoCode
            {
                UserId = request.UserId,
                PromoCodeId = promoCode.Id
            };

            _db.UserPromoCodes.Add(userPromoCode);
            await _db.SaveChangesAsync(ct);

            return new CreatePromoCodeResponse
            {
                PromoCodeId = promoCode.Id,
                Code = promoCode.Code,
                DiscountType = promoCode.DiscountType,
                DiscountValue = promoCode.DiscountValue,
                UserId = request.UserId,
                ExpireRideCount = request.ExpireRideCount
            };
        }

        public async Task<ApplyPromoCodeResponse> ApplyAsync(ApplyPromoCodeRequest request, CancellationToken ct = default)
        {
            var userPromoCode = await _db.UserPromoCodes
                .Include(up => up.PromoCode)
                .FirstOrDefaultAsync(up => up.PromoCodeId == request.PromoCodeId && up.UserId == request.UserId, ct);

            if (userPromoCode == null)
                throw new InvalidOperationException("Promo code is not assigned to this user.");

            var promoCode = userPromoCode.PromoCode;
            if (!promoCode.IsActive)
                throw new InvalidOperationException("Promo code is not active.");

            var now = DateTime.UtcNow;
            if (promoCode.StartDate.HasValue && promoCode.StartDate > now)
                throw new InvalidOperationException("Promo code is not active yet.");

            if (promoCode.EndDate.HasValue && promoCode.EndDate < now)
                throw new InvalidOperationException("Promo code is expired.");

            if (promoCode.MaxUsagePerUser.HasValue && userPromoCode.UsedCount >= promoCode.MaxUsagePerUser)
                throw new InvalidOperationException("Promo code usage limit reached for this user.");

            if (promoCode.MaxUsageCount.HasValue)
            {
                var totalUsage = await _db.UserPromoCodes
                    .Where(up => up.PromoCodeId == promoCode.Id)
                    .SumAsync(up => up.UsedCount, ct);

                if (totalUsage >= promoCode.MaxUsageCount)
                    throw new InvalidOperationException("Promo code usage limit reached.");
            }

            var originalFare = request.OriginalFare;
            if (originalFare < 0)
                throw new InvalidOperationException("Original fare must be zero or greater.");

            var discountAmount = CalculateDiscount(originalFare, promoCode);
            var finalFare = Math.Max(0, originalFare - discountAmount);

            userPromoCode.UsedCount += 1;
            userPromoCode.LastUsedTime = now;

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Promo code {PromoCodeId} applied for user {UserId}", promoCode.Id, request.UserId);

            return new ApplyPromoCodeResponse
            {
                UserId = request.UserId,
                PromoCodeId = promoCode.Id,
                OriginalFare = originalFare,
                DiscountAmount = discountAmount,
                FinalFare = finalFare
            };
        }

        public async Task DeleteAsync(int promoCodeId, CancellationToken ct = default)
        {
            var promoCode = await _db.PromoCodes.FirstOrDefaultAsync(p => p.Id == promoCodeId, ct);
            if (promoCode == null)
                throw new KeyNotFoundException($"Promo code #{promoCodeId} not found");

            promoCode.IsActive = false;
            await _db.SaveChangesAsync(ct);
        }

        private static decimal CalculateDiscount(decimal originalFare, PromoCode promoCode)
        {
            if (originalFare <= 0)
                return 0;

            decimal discount;

            if (promoCode.DiscountType == PromoDiscountType.Percentage)
            {
                discount = originalFare * promoCode.DiscountValue / 100m;
                if (promoCode.MaxDiscountAmount.HasValue && discount > promoCode.MaxDiscountAmount)
                    discount = promoCode.MaxDiscountAmount.Value;
            }
            else
            {
                discount = promoCode.DiscountValue;
            }

            return Math.Clamp(discount, 0, originalFare);
        }
    }
}
