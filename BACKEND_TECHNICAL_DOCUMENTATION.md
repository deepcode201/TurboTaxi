# TurboTaxi Backend - Teknik Implementasyon Dokümantasyonu

## 🏗️ Architecture Overview

```
┌─────────────────┐
│  React Native   │
│  (User/Driver)  │
└────────┬────────┘
         │ SignalR
         │ REST API
         ↓
┌─────────────────────────────────────┐
│    TurboTaxi.API (Controllers)      │
│  ┌─────────────────────────────────┐│
│  │ - RidesController               ││
│  │ - TripHistoryController         ││
│  │ - PromoCodesController          ││
│  └─────────────────────────────────┘│
└────────┬────────────────────────────┘
         ↓
┌─────────────────────────────────────┐
│  TurboTaxi.Infrastructure           │
│  ┌─────────────────────────────────┐│
│  │ Services:                       ││
│  │ - RideService                   ││
│  │ - FareCalculatorService         ││
│  │ - RouteEstimationService        ││
│  │ - PromoCodeService              ││
│  └─────────────────────────────────┘│
└────────┬────────────────────────────┘
         ↓
┌─────────────────────────────────────┐
│  TurboTaxi.Realtime (SignalR)       │
│  ┌─────────────────────────────────┐│
│  │ Hubs:                           ││
│  │ - DriverHub                     ││
│  │ - UserHub                       ││
│  └─────────────────────────────────┘│
└────────┬────────────────────────────┘
         ↓
┌─────────────────────────────────────┐
│  Infrastructure Layer               │
│  ┌─────────────────────────────────┐│
│  │ - PostgreSQL Database           ││
│  │ - Redis Cache                   ││
│  │ - Google Maps API               ││
│  └─────────────────────────────────┘│
└─────────────────────────────────────┘
```

---

## 📋 Değişiklikler Özeti

### 1. Polyline Fallback Sistemi

#### Dosya: `RideService.cs`

```csharp
private async Task<(string? polyline, double? distanceKm, int? durationMin)> EstimateRouteAsync(...)
{
    try
    {
        var response = await _routeEstimation.EstimateAsync(...);
        
        if (response.Success && response.Data != null)
        {
            return (response.Data.EncodedPolyline, response.Data.DistanceKm, response.Data.DurationMinutes);
        }
        
        // ✅ FALLBACK: Google Maps başarısız olsa bile polyline oluştur
        double approximateDistanceKm = CalculateApproximateDistance(...);
        int approximateDurationMin = (int)Math.Round(approximateDistanceKm * 2);
        
        // ✅ YENI: Simple polyline oluştur
        string fallbackPolyline = CreateSimplePolyline(originLat, originLng, destLat, destLng);
        
        return (fallbackPolyline, approximateDistanceKm, approximateDurationMin);
    }
    catch (Exception ex)
    {
        // Fallback ile dönüş
    }
}

// ✅ YENI: Google Maps Polyline Encoding Algoritması
private static string CreateSimplePolyline(double lat1, double lon1, double lat2, double lon2)
{
    List<(double lat, double lng)> points = new()
    {
        (lat1, lon1),
        (lat2, lon2)
    };
    return EncodePolyline(points);
}

private static string EncodePolyline(List<(double lat, double lng)> points)
{
    var encoded = new StringBuilder();
    int prevLat = 0;
    int prevLng = 0;

    foreach (var (lat, lng) in points)
    {
        int currLat = (int)Math.Round(lat * 1e5);
        int currLng = (int)Math.Round(lng * 1e5);

        EncodeValue(encoded, currLat - prevLat);
        EncodeValue(encoded, currLng - prevLng);

        prevLat = currLat;
        prevLng = currLng;
    }

    return encoded.ToString();
}

private static void EncodeValue(StringBuilder encoded, int value)
{
    value = value << 1;
    if (value < 0) value = ~value;

    while (value >= 0x20)
    {
        int chunk = (0x20 | (value & 0x1f)) + 63;
        encoded.Append((char)chunk);
        value >>= 5;
    }

    encoded.Append((char)(value + 63));
}

// ✅ YENI: Haversine Formülü ile Mesafe Hesabı
private static double CalculateApproximateDistance(double lat1, double lon1, double lat2, double lon2)
{
    const double R = 6371; // Earth's radius in kilometers
    double dLat = DegreesToRadians(lat2 - lat1);
    double dLon = DegreesToRadians(lon2 - lon1);
    
    double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
               Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
               Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
    
    double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    double distance = R * c;
    
    return Math.Round(distance, 2);
}
```

---

### 2. User Cancel - Driver Notification Silinme

#### Dosya: `RideService.cs` - CancelRideByUserAsync

```csharp
public async Task<CancelRideResponse> CancelRideByUserAsync(int rideId, CancelRideByUserRequest request, CancellationToken ct = default)
{
    var ride = await _db.Rides
        .Include(r => r.StartLocation)
        .Include(r => r.EndLocation)
        .FirstOrDefaultAsync(r => r.Id == rideId, ct);

    // Ride Status'u Canceled'a çek
    ride.Status = RideStatus.Canceled;
    ride.CanceledTime = DateTime.UtcNow;
    await _db.SaveChangesAsync(ct);

    // ✅ Driver'ı serbest bırak (eğer qebul etmişse)
    if (ride.DriverId.HasValue)
    {
        try
        {
            // Redis'ten driver state'ini temizle
            var hKey = RedisKeys.DriverHash(ride.DriverId.Value);
            await _redis.HashDeleteAsync(hKey, "currentRideId");
            await _redis.HashDeleteAsync(hKey, "currentUserId");
            
            // Driver'a cancel notification gönder
            var driverCancelPayload = new RideCompletionPayload
            {
                RideId = ride.Id,
                Status = ride.Status.ToString(),
                ClearMap = true,
                Message = "User canceled this ride"
            };
            await _notification.NotifyDriverAsync(ride.DriverId.Value, driverCancelPayload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to cancel for driver #{ride.DriverId}");
        }
    }

    // ✅ YENI: TÜM driver'lara broadcast - Notification'ı UI'dan kaldır
    try
    {
        // Redis notification keys'lerini temizle
        await _redis.DeleteKeysByPatternAsync($"notify:ride:{rideId}:driver:*");
        
        // ✅ KRITIK: Tüm driver'lara "sipariş iptal edildi" mesajı
        await _notification.NotifyAllDriversRideCanceled(rideId);
        
        _logger.LogInformation($"✓ All drivers notified about ride #{rideId} cancellation");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to notify all drivers");
    }

    return new CancelRideResponse
    {
        RideId = ride.Id,
        Status = ride.Status.ToString()
    };
}
```

#### Dosya: `DriverHub.cs` - Driver Auto-Subscribe

```csharp
public override async Task OnConnectedAsync()
{
    _logger.LogInformation($"[SignalR] Driver connected: {Context.ConnectionId}");
    
    // ✅ YENI: Tüm broadcast mesajları almak için auto-subscribe
    await Groups.AddToGroupAsync(Context.ConnectionId, "all_drivers");
    _logger.LogInformation($"[SignalR] Driver {Context.ConnectionId} added to 'all_drivers' group");
    
    await base.OnConnectedAsync();
}
```

---

### 3. Şəhər Əsasında Qiymət Sistemi

#### Dosya: `FareCalculatorService.cs`

```csharp
public async Task<decimal> CalculateEstimatedFareAsync(
    string vehicleType,
    double? distanceKm,
    int? durationMinutes,
    DateTime? rideStartTime = null,
    string? city = "Baku")  // ✅ YENI PARAMETER
{
    // ... validation ...

    // ✅ YENI: Şəhərə göre tariff axtarış
    var tariff = await _db.Tariffs
        .AsNoTracking()
        .FirstOrDefaultAsync(t => 
            t.VehicleType == parsedVehicleType && 
            t.City == city &&  // ← Şəhər filtrosu
            t.IsActive);

    // Fallback: Eğer şəhir-spesifik tariff yoksa, hər tariff al
    if (tariff == null)
    {
        tariff = await _db.Tariffs
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.VehicleType == parsedVehicleType && t.IsActive);
    }

    // Fare hesablaması
    decimal fare = tariff.BaseFare;
    fare += (decimal)(chargeableKm * (double)tariff.PricePerKm);
    fare += (decimal)(chargeableMinutes * (double)tariff.PricePerMinute);

    // Gece multiplier'ı
    if (rideStartTime.HasValue && tariff.NightMultiplier.HasValue && IsNightTime(rideStartTime.Value, tariff))
    {
        fare *= tariff.NightMultiplier.Value;
    }

    return Math.Max(fare, tariff.MinimumFare);
}
```

#### Dosya: `Migration - AddCityBasedTariffing.cs`

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // ✅ Bakı üçün Eco Tariff
    migrationBuilder.InsertData(
        table: "Tariffs",
        columns: new[] { "Country", "City", "VehicleType", "BaseFare", "PricePerKm", "PricePerMinute", "MinimumFare", "IsActive" },
        values: new object[] { "AZ", "Baku", 0, 2.0m, 0.30m, 0.15m, 5.0m, true });

    // ✅ Bakı üçün Comfort Tariff
    migrationBuilder.InsertData(
        table: "Tariffs",
        columns: new[] { "Country", "City", "VehicleType", "BaseFare", "PricePerKm", "PricePerMinute", "MinimumFare", "IsActive" },
        values: new object[] { "AZ", "Baku", 1, 3.0m, 0.40m, 0.20m, 7.0m, true });

    // ✅ Bakı üçün Premium Tariff
    migrationBuilder.InsertData(
        table: "Tariffs",
        columns: new[] { "Country", "City", "VehicleType", "BaseFare", "PricePerKm", "PricePerMinute", "MinimumFare", "IsActive" },
        values: new object[] { "AZ", "Baku", 2, 5.0m, 0.60m, 0.30m, 10.0m, true });
}
```

---

### 4. Promo Code Sistemi

#### Dosya: `RideService.cs` - Trip History ile Promo Code

```csharp
public async Task<PagedResult<UserTripHistoryItem>> GetUserTripHistoryAsync(int userId, TripHistoryQuery query, CancellationToken ct = default)
{
    // ... pagination ...

    // ✅ YENI: User'ın aktif promo code'u al
    var activePromoCode = await _db.UserPromoCodes
        .AsNoTracking()
        .Include(up => up.PromoCode)
        .Where(up => up.UserId == userId && up.PromoCode.IsActive)
        .FirstOrDefaultAsync(ct);

    var items = rides.Select(r => new UserTripHistoryItem
    {
        RideId = r.Id,
        Fare = r.ActualFare ?? r.EstimatedFare ?? 0,
        
        // ✅ YENI: Discount ve Final Fare hesapla
        DiscountAmount = activePromoCode != null 
            ? CalculateDiscountAmount(r.ActualFare ?? r.EstimatedFare ?? 0, activePromoCode.PromoCode) 
            : null,
        FinalFare = activePromoCode != null 
            ? Math.Max(0, (r.ActualFare ?? r.EstimatedFare ?? 0) - CalculateDiscountAmount(r.ActualFare ?? r.EstimatedFare ?? 0, activePromoCode.PromoCode)) 
            : null,
        
        Status = r.Status.ToString()
    }).ToList();

    return new PagedResult<UserTripHistoryItem>
    {
        Items = items,
        PageNumber = pageNumber,
        PageSize = pageSize,
        TotalCount = totalCount,
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
    };
}

// ✅ YENI: Discount hesaplama helper
private static decimal CalculateDiscountAmount(decimal originalFare, PromoCode promoCode)
{
    if (originalFare <= 0) return 0;

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
```

#### Dosya: `TripHistoryModels.cs`

```csharp
public class UserTripHistoryItem
{
    public int RideId { get; set; }
    public string? DriverName { get; set; }
    public decimal Fare { get; set; }
    
    // ✅ YENI: Discount alanları
    public decimal? DiscountAmount { get; set; }    // İndirim miktarı
    public decimal? FinalFare { get; set; }         // İndirimli fiyat
    
    public DateTime? RideDateUtc { get; set; }
    public string Status { get; set; } = null!;
}
```

---

### 5. Ride Models - Polyline Expansion

#### Dosya: `RideModels.cs`

```csharp
public class RideCompletionPayload
{
    public int RideId { get; set; }
    public string Status { get; set; } = "Completed";
    public decimal FinalPrice { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public bool ClearMap { get; set; } = true;
    public string? Message { get; set; }
    public string? UserType { get; set; }
    
    // ✅ YENI: Polyline ve route bilgileri
    public string? EncodedPolyline { get; set; }     // Google Maps encoded
    public double? DistanceKm { get; set; }          // Mesafe
    public int? DurationMinutes { get; set; }        // Süre
}
```

---

## 🔄 Request/Response Flow'ları

### 1. Yeni Sipariş Oluşturma

```
User Frontend
    │
    ├─ POST /api/rides/create
    │   {
    │     "userId": 1,
    │     "pickupLat": 40.4093,
    │     "pickupLng": 49.8599,
    │     "destinationLat": 40.3856,
    │     "destinationLng": 49.8989,
    │     "vehicleType": "Eco"
    │   }
    │
    └─→ RideService.CreateRideAsync()
         │
         ├─ Route Estimation (Google Maps API)
         │  │
         │  └─ Success ✓: EncodedPolyline, DistanceKm, DurationMinutes
         │     OR
         │  └─ Fail ✗: Fallback Polyline + Haversine Distance
         │
         ├─ Fare Calculation (Bakı Tariff'i ile)
         │  └─ BaseFare + (DistanceKm * PricePerKm) + (DurationMin * PricePerMin)
         │
         ├─ Find Nearby Drivers (Redis Geo)
         │
         ├─ Create Ride in DB
         │
         └─ NotifyNearestDrivers() via SignalR
            │
            └─→ DriverHub.NewRideRequest
                {
                  "rideId": 123,
                  "vehicleType": "Eco",
                  "pickupLat": 40.4093,
                  "pickupLng": 49.8599,
                  "estimatedFare": 5.50,
                  "distanceKm": 6.25,
                  "durationMinutes": 12,
                  "encodedPolyline": "..." ✅
                }

Response:
{
  "rideId": 123,
  "status": "Pending",
  "estimatedFare": 5.50,
  "distanceKm": 6.25,
  "durationMinutes": 12
}
```

### 2. Driver Qebul Etme

```
Driver Frontend (accepts ride)
    │
    └─ POST /api/rides/{rideId}/accept
       {
         "driverId": 5
       }
       │
       └─→ RideService.AcceptRideAsync()
           │
           ├─ Get Driver Location (Redis)
           │
           ├─ Route Estimation (Driver → Pickup)
           │  └─ Polyline + Distance + Duration
           │
           ├─ Update Ride Status: Pending → Approved
           │
           ├─ Update Driver Status in Redis: Busy
           │
           └─ NotifyUser() via SignalR
              │
              └─→ UserHub.RideStatusUpdated
                  {
                    "rideId": 123,
                    "status": "Approved",
                    "driverId": 5,
                    "driverLat": 40.415,
                    "driverLng": 49.860,
                    "encodedPolyline": "...", ✅ Driver → Pickup
                    "distanceKm": 2.3,
                    "durationMinutes": 5
                  }
```

### 3. User İptal Etme

```
User Frontend (cancels ride)
    │
    └─ POST /api/rides/{rideId}/cancel
       │
       └─→ RideService.CancelRideByUserAsync()
           │
           ├─ Update Ride Status: * → Canceled
           │
           ├─ (If Driver accepted)
           │  └─ Free Driver in Redis
           │
           ├─ NotifyDriver() about cancellation
           │  └─→ DriverHub.RideStatusUpdated ("Canceled")
           │
           ├─ ✅ YENI: Clean Redis notification keys
           │
           └─ ✅ YENI: NotifyAllDriversRideCanceled()
              │
              └─→ All Drivers (in "all_drivers" group)
                  │
                  └─→ DriverHub.RideStatusUpdated
                      {
                        "rideId": 123,
                        "status": "Canceled",
                        "message": "This ride has been canceled"
                      }
                      │
                      └─ Frontend: Remove notification from list
```

### 4. Trip History ile Promo Code

```
User Frontend
    │
    └─ GET /api/trip-history/user/{userId}
       │
       └─→ RideService.GetUserTripHistoryAsync()
           │
           ├─ Get all user rides
           │
           ├─ ✅ Get active promo code (if exists)
           │
           └─ For each ride:
              ├─ Original Fare
              ├─ ✅ Calculate Discount (if promo code)
              └─ ✅ Calculate Final Fare
                 
Response:
{
  "items": [
    {
      "rideId": 123,
      "fare": 10.00,           // Original
      "discountAmount": 1.00,  // ✅ Promo code discount
      "finalFare": 9.00,       // ✅ Final price
      "status": "Completed"
    }
  ]
}
```

---

## 🗄️ Database Schema (Relevant Tables)

```sql
-- Tariffs (Qiymət cədvəli)
CREATE TABLE Tariffs (
    Id SERIAL PRIMARY KEY,
    Country VARCHAR(3),           -- "AZ"
    City VARCHAR(50),             -- "Baku"
    VehicleType INT,              -- 0=Eco, 1=Comfort, 2=Premium
    BaseFare DECIMAL(10,2),       -- Açılış ücreti
    PricePerKm DECIMAL(10,2),     -- KM başına
    PricePerMinute DECIMAL(10,2), -- Dəqiqə başına
    MinimumFare DECIMAL(10,2),    -- Minimum ödəniş
    IsActive BOOLEAN
);

-- Rides
CREATE TABLE Rides (
    Id SERIAL PRIMARY KEY,
    UserId INT,
    DriverId INT,
    StartLocationId INT,
    EndLocationId INT,
    Status VARCHAR(20),           -- "Pending", "Approved", "OnWay", "Completed", "Canceled"
    RequestedTime TIMESTAMP,
    AcceptedTime TIMESTAMP,
    PickUpTime TIMESTAMP,
    CompletedTime TIMESTAMP,
    CanceledTime TIMESTAMP,
    EstimatedFare DECIMAL(10,2),
    ActualFare DECIMAL(10,2),
    RoutePolyline TEXT,           -- ✅ Google Maps encoded
    EstimatedDistanceKm DOUBLE,   -- ✅ Mesafe
    EstimatedDurationMinutes INT  -- ✅ Müddet
);

-- Promo Codes
CREATE TABLE PromoCodes (
    Id SERIAL PRIMARY KEY,
    Code VARCHAR(100),
    DiscountType INT,             -- 0=Percentage, 1=FixedAmount
    DiscountValue DECIMAL(10,2),
    MaxDiscountAmount DECIMAL(10,2),
    IsActive BOOLEAN
);

-- User Promo Codes
CREATE TABLE UserPromoCodes (
    Id SERIAL PRIMARY KEY,
    UserId INT,
    PromoCodeId INT,
    UsedCount INT DEFAULT 0,
    LastUsedTime TIMESTAMP
);
```

---

## 🚀 Deployment

### Environment Setup

```bash
# .env file
DATABASE_URL=postgresql://user:pass@localhost:5432/turbotaxi
REDIS_URL=redis://localhost:6379
GOOGLE_MAPS_API_KEY=your_key

# Logging
LOG_LEVEL=Information
```

### Database Migration

```bash
cd TurboTaxi.Infrastructure
dotnet ef database update
```

### Run Application

```bash
dotnet run --configuration Release
```

---

## 📊 Monitoring & Logging

### Log Levels

```csharp
// Important logs to monitor:

// ✅ Route Estimation Success
_logger.LogInformation($"? Route: {response.Data.DistanceKm:F2} km");

// ⚠️ Route Estimation Fallback
_logger.LogWarning("?? Route estimation returned no valid data, using fallback");

// ✓ SignalR Events
_logger.LogInformation($"[SignalR] Driver connected: {Context.ConnectionId}");
_logger.LogInformation($"[SignalR] RideStatusUpdated: rideId {rideId}");

// ✗ Errors
_logger.LogError(ex, "Failed to notify drivers");
```

---

**Document Version:** 1.0  
**Last Updated:** 2025-02-26  
**.NET Version:** 10  
**Database:** PostgreSQL  
**Cache:** Redis
