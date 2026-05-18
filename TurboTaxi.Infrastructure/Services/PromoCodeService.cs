using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Domain.Entities;
using TurboTaxi.Domain.Enums;
using TurboTaxi.Infrastructure.Data;
using TurboTaxi.Models.PromoCodes;
using TurboTaxi.Models.Trips;

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

            await ValidatePromoCanBeUsedAsync(userPromoCode, userPromoCode.PromoCode, ct);

            var originalFare = request.OriginalFare;
            if (originalFare < 0)
                throw new InvalidOperationException("Original fare must be zero or greater.");

            var discountAmount = CalculateDiscount(originalFare, userPromoCode.PromoCode);
            var finalFare = Math.Max(0, originalFare - discountAmount);

            await ConsumePromoUsageAsync(userPromoCode, userPromoCode.PromoCode, ct);

            _logger.LogInformation("Promo code {PromoCodeId} applied for user {UserId}", userPromoCode.PromoCode.Id, request.UserId);

            return new ApplyPromoCodeResponse
            {
                UserId = request.UserId,
                PromoCodeId = userPromoCode.PromoCode.Id,
                OriginalFare = originalFare,
                DiscountAmount = discountAmount,
                FinalFare = finalFare
            };
        }

        public async Task<PromoFareResult> ApplyActivePromoAsync(
            int userId,
            decimal originalFare,
            bool consumeUsage,
            CancellationToken ct = default)
        {
            if (originalFare <= 0)
            {
                return new PromoFareResult
                {
                    Applied = false,
                    OriginalFare = originalFare,
                    FinalFare = originalFare
                };
            }

            var userPromoCode = await FindActiveUserPromoAsync(userId, ct);
            if (userPromoCode == null)
            {
                return new PromoFareResult
                {
                    Applied = false,
                    OriginalFare = originalFare,
                    FinalFare = originalFare
                };
            }

            var promoCode = userPromoCode.PromoCode;
            var discountAmount = CalculateDiscount(originalFare, promoCode);
            var finalFare = Math.Max(0, originalFare - discountAmount);

            int? remaining = null;
            if (consumeUsage)
            {
                await ConsumePromoUsageAsync(userPromoCode, promoCode, ct);
                remaining = GetRemainingRideCount(userPromoCode, promoCode);
                _logger.LogInformation(
                    "Active promo {PromoCodeId} consumed for user {UserId}. Remaining rides: {Remaining}",
                    promoCode.Id, userId, remaining);
            }
            else
            {
                remaining = GetRemainingRideCount(userPromoCode, promoCode);
            }

            return new PromoFareResult
            {
                Applied = true,
                PromoCodeId = promoCode.Id,
                PromoCode = promoCode.Code,
                OriginalFare = originalFare,
                DiscountAmount = discountAmount,
                FinalFare = finalFare,
                RemainingRideCount = remaining
            };
        }

        private async Task<UserPromoCode?> FindActiveUserPromoAsync(int userId, CancellationToken ct)
        {
            var candidates = await _db.UserPromoCodes
                .Include(up => up.PromoCode)
                .Where(up => up.UserId == userId && up.PromoCode.IsActive)
                .OrderByDescending(up => up.CreatedTime)
                .ToListAsync(ct);

            foreach (var userPromo in candidates)
            {
                try
                {
                    await ValidatePromoCanBeUsedAsync(userPromo, userPromo.PromoCode, ct);
                    return userPromo;
                }
                catch (InvalidOperationException)
                {
                    // skip invalid / exhausted promos
                }
            }

            return null;
        }

        private async Task ValidatePromoCanBeUsedAsync(UserPromoCode userPromoCode, PromoCode promoCode, CancellationToken ct)
        {
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
        }

        private async Task ConsumePromoUsageAsync(UserPromoCode userPromoCode, PromoCode promoCode, CancellationToken ct)
        {
            userPromoCode.UsedCount += 1;
            userPromoCode.LastUsedTime = DateTime.UtcNow;

            if (promoCode.MaxUsagePerUser.HasValue &&
                userPromoCode.UsedCount >= promoCode.MaxUsagePerUser)
            {
                promoCode.IsActive = false;
                _logger.LogInformation(
                    "Promo code {PromoCodeId} expired for user {UserId} (ride limit reached)",
                    promoCode.Id, userPromoCode.UserId);
            }

            await _db.SaveChangesAsync(ct);
        }

        private static int? GetRemainingRideCount(UserPromoCode userPromoCode, PromoCode promoCode)
        {
            if (!promoCode.MaxUsagePerUser.HasValue)
                return null;

            return Math.Max(0, promoCode.MaxUsagePerUser.Value - userPromoCode.UsedCount);
        }

        public async Task DeleteAsync(int promoCodeId, CancellationToken ct = default)
        {
            var promoCode = await _db.PromoCodes.FirstOrDefaultAsync(p => p.Id == promoCodeId, ct);
            if (promoCode == null)
                throw new KeyNotFoundException($"Promo code #{promoCodeId} not found");

            promoCode.IsActive = false;
            await _db.SaveChangesAsync(ct);
        }

        public async Task<PagedResult<UserPromoCodeDto>> GetUserPromoCodesAsync(
            PromoCodeListQuery query,
            CancellationToken ct = default)
        {
            if (query.UserId.HasValue)
            {
                var userExists = await _db.Users.AnyAsync(u => u.Id == query.UserId.Value, ct);
                if (!userExists)
                    throw new KeyNotFoundException($"User #{query.UserId.Value} not found");
            }

            var pageNumber = NormalizePageNumber(query.PageNumber);
            var pageSize = NormalizePageSize(query.PageSize);

            var dbQuery = _db.UserPromoCodes
                .AsNoTracking()
                .Include(up => up.PromoCode)
                .AsQueryable();

            if (query.UserId.HasValue)
                dbQuery = dbQuery.Where(up => up.UserId == query.UserId.Value);

            if (query.ActiveOnly == true)
            {
                var now = DateTime.UtcNow;
                dbQuery = dbQuery.Where(up =>
                    up.PromoCode.IsActive
                    && (!up.PromoCode.StartDate.HasValue || up.PromoCode.StartDate <= now)
                    && (!up.PromoCode.EndDate.HasValue || up.PromoCode.EndDate >= now)
                    && (!up.PromoCode.MaxUsagePerUser.HasValue || up.UsedCount < up.PromoCode.MaxUsagePerUser));
            }

            var totalCount = await dbQuery.CountAsync(ct);

            var rows = await dbQuery
                .OrderByDescending(up => up.CreatedTime)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var items = rows.Select(MapToUserPromoCodeDto).ToList();

            return new PagedResult<UserPromoCodeDto>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        private static UserPromoCodeDto MapToUserPromoCodeDto(UserPromoCode up)
        {
            var p = up.PromoCode;
            return new UserPromoCodeDto
            {
                UserPromoCodeId = up.Id,
                UserId = up.UserId,
                PromoCodeId = p.Id,
                Code = p.Code,
                Description = p.Description,
                DiscountType = p.DiscountType,
                DiscountValue = p.DiscountValue,
                MaxDiscountAmount = p.MaxDiscountAmount,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                MaxUsagePerUser = p.MaxUsagePerUser,
                MaxUsageCount = p.MaxUsageCount,
                UsedCount = up.UsedCount,
                RemainingRideCount = GetRemainingRideCount(up, p),
                IsActive = p.IsActive,
                CanUse = CanUsePromo(up, p),
                LastUsedTime = up.LastUsedTime,
                CreatedTime = up.CreatedTime
            };
        }

        private static bool CanUsePromo(UserPromoCode userPromoCode, PromoCode promoCode)
        {
            if (!promoCode.IsActive)
                return false;

            var now = DateTime.UtcNow;
            if (promoCode.StartDate.HasValue && promoCode.StartDate > now)
                return false;
            if (promoCode.EndDate.HasValue && promoCode.EndDate < now)
                return false;
            if (promoCode.MaxUsagePerUser.HasValue && userPromoCode.UsedCount >= promoCode.MaxUsagePerUser)
                return false;

            return true;
        }

        private static int NormalizePageNumber(int pageNumber) => pageNumber < 1 ? 1 : pageNumber;

        private static int NormalizePageSize(int pageSize)
        {
            if (pageSize < 1)
                return 20;
            return pageSize > 100 ? 100 : pageSize;
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
