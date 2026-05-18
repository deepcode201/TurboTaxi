# TurboTaxi - Complete Implementation Guide

## 📚 Documentation Files

Bu repository'de 3 ana dokumentasyon file'ı bulunmaktadır:

### 1. 🎯 **REACT_NATIVE_INTEGRATION_GUIDE.md**
**React Native Developer'lar için**
- Frontend implementasyon örnekleri
- Polyline rendering
- SignalR event handling
- Promo code UI
- Error handling

👉 **Bunu oku eğer React Native client geliştiriyorsan**

### 2. 🏗️ **BACKEND_TECHNICAL_DOCUMENTATION.md**
**.NET Backend Mimarı için**
- C# source code explanations
- Database schema
- API flow diagrams
- Migration scripts
- Deployment guide

👉 **Bunu oku eğer Backend mantığını anlamak istiyorsan**

### 3. 📄 **README.md** (Bu dosya)
Hızlı başlangıç ve overview

---

## 🎯 Yapılan Düzeltmeler

### ✅ 1. Polyline Problemi
**Status:** ÇÖZÜLDÜ

**Problem:** Driver qebul ettiğinde, User'ın haritasında polyline tuhaf görünüyordu.

**Çözüm:**
- Google Maps API başarısız olsa bile fallback polyline oluşturuluyor
- Haversine formülü ile yaklaşık mesafe hesaplanıyor
- Polyline **asla null değil** - her zaman bir görüntü gönderiliyor

**Detaylar:** `BACKEND_TECHNICAL_DOCUMENTATION.md` → Section 1

---

### ✅ 2. User Cancel - Driver Notification
**Status:** ÇÖZÜLDÜ

**Problem:** User sipariş iptal ettiğinde, Driver'ın notification'ı silinmiyor.

**Çözüm:**
- Driver'lar bağlandıklarında `"all_drivers"` grup'una auto-subscribe oluyor
- User iptal ettiğinde, TÜM driver'lara broadcast mesajı gidiyor
- Frontend bu mesajı alıp notification'ı list'ten kaldırıyor

**Detaylar:** `BACKEND_TECHNICAL_DOCUMENTATION.md` → Section 2

---

### ✅ 3. Mesafe ve Müddet Sorunu
**Status:** ÇÖZÜLDÜ

**Problem:** Driver'a Order type çıkıyor ama Distance/Duration null oluyordu.

**Çözüm:**
- Fallback calculation eklendi
- Google Maps başarısız olsa bile ~2 dk/km tahmini yapılıyor
- Distance ve Duration **asla null değil**

**Detaylar:** `BACKEND_TECHNICAL_DOCUMENTATION.md` → Section 3

---

### ✅ 4. Şəhər Əsasında Qiymət
**Status:** ÇÖZÜLDÜ

**Problem:** Tüm şəhirlər için aynı tariff kullanılıyordu.

**Çözüm:**
- `Tariff` tablosunda `City` column'ı var
- FareCalculator şəhirə göre tariff axtarıyor
- Bakı üçün tariffler migrate edildi:
  - Eco: 2 AZN açılış
  - Comfort: 3 AZN açılış
  - Premium: 5 AZN açılış

**Detaylar:** `BACKEND_TECHNICAL_DOCUMENTATION.md` → Section 4

---

### ✅ 5. Promo Code Sistemi
**Status:** ÇÖZÜLDÜ

**Problem:** Trip history'de promo code discount gösterilmiyor.

**Çözüm:**
- Trip history endpoint'i promo code discount hesaplıyor
- User'ın aktif promo code'u varsa, discount gösterilir
- `UserTripHistoryItem` sınıfına `DiscountAmount` ve `FinalFare` alanları eklendi

**Detaylar:** `BACKEND_TECHNICAL_DOCUMENTATION.md` → Section 5

---

## 🚀 Quick Start

### Backend Setup

```bash
# 1. Clone repository
git clone https://github.com/deepcode201/TurboTaxi.git
cd TurboTaxi

# 2. Database migrations
cd TurboTaxi.Infrastructure
dotnet ef database update

# 3. Run backend
dotnet run --configuration Release
```

### React Native Setup

```bash
# 1. Install dependencies
npm install
# or
yarn install

# 2. Configure environment
cp .env.example .env
# Edit .env with your API URLs

# 3. Run app
npm start

# For iOS
cd ios && pod install && cd ..
npx react-native run-ios

# For Android
npx react-native run-android
```

---

## 📋 API Endpoints

### User Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/rides/create` | Yeni sipariş oluştur |
| POST | `/api/rides/{rideId}/cancel` | Sipariş iptal et |
| GET | `/api/trip-history/user/{userId}` | Trip history (promo code ile) |
| POST | `/api/promo-codes/apply` | Promo code uygula |

### Driver Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/rides/{rideId}/accept` | Sipariş qebul et |
| POST | `/api/rides/{rideId}/start` | Seyahate başla |
| POST | `/api/rides/{rideId}/finish` | Seyahati bitir |
| GET | `/api/trip-history/driver/{driverId}` | Driver trip history |

---

## 🔌 SignalR Events

### Driver Hub Events (Receiving)

```
NewRideRequest
├─ Params: requestId, payload
├─ Payload: { rideId, vehicleType, pickupLat, pickupLng, distanceKm, durationMinutes, encodedPolyline, ... }
└─ When: New ride request

RideStatusUpdated
├─ Params: rideId, status, payload
├─ Payload: { Status: "Approved" | "OnWay" | "Completed" | "Canceled", ... }
└─ When: Any ride status change
```

### User Hub Events (Receiving)

```
RideStatusUpdated
├─ Params: rideId, status, payload
├─ Payload: { driverId, driverLat, driverLng, encodedPolyline, ... }
└─ When: Any ride status change
```

---

## 🗄️ Database Models

### Key Tables

- **Rides** - Tüm seferler (ID, UserId, DriverId, Status, Fare, Polyline, Distance, Duration)
- **Tariffs** - Şəhir-spesifik qiymətlər (City, VehicleType, BaseFare, PricePerKm, etc.)
- **PromoCodes** - Promo kodlar (Code, DiscountType, DiscountValue)
- **UserPromoCodes** - User-PromoCode ilişkileri (UserId, PromoCodeId, UsedCount)
- **Locations** - GPS koordinatları (Latitude, Longitude, City)
- **Users** - User profilleri
- **Drivers** - Driver profilleri

---

## 🔧 Key Components

### Backend Services

| Service | Responsibility |
|---------|-----------------|
| `RideService` | Ride lifecycle (create, accept, start, finish, cancel) |
| `FareCalculatorService` | Fare hesablaması (şəhir-spesifik) |
| `RouteEstimationService` | Google Maps API integration + fallback |
| `PromoCodeService` | Promo code management |
| `DriverLocationService` | Real-time driver location tracking |
| `RideNotificationService` | SignalR notifications |

### Frontend Components

| Component | Purpose |
|-----------|---------|
| `RideListScreen` | Active rides (with SignalR listeners) |
| `RideMapScreen` | Map with polyline, markers |
| `TripHistoryScreen` | Completed rides (with discount info) |
| `PromoCodeScreen` | Promo code application |

---

## 📊 Architecture Diagram

```
┌─────────────────────────────────────────────────┐
│         React Native Frontend                   │
│  ┌─────────────────────────────────────────┐    │
│  │ • RideListScreen                        │    │
│  │ • RideMapScreen (Polyline rendering)    │    │
│  │ • TripHistoryScreen (Promo discount)    │    │
│  │ • PromoCodeScreen                       │    │
│  └─────────────────────────────────────────┘    │
└────────────────┬─────────────────────────────────┘
                 │ SignalR + REST API
                 ↓
┌─────────────────────────────────────────────────┐
│    TurboTaxi.API (ASP.NET Core Controllers)     │
│  ┌─────────────────────────────────────────┐    │
│  │ • RidesController                       │    │
│  │ • TripHistoryController                 │    │
│  │ • PromoCodesController                  │    │
│  └─────────────────────────────────────────┘    │
└────────────────┬─────────────────────────────────┘
                 │
                 ↓
┌─────────────────────────────────────────────────┐
│    TurboTaxi.Infrastructure (Services)          │
│  ┌─────────────────────────────────────────┐    │
│  │ • RideService                           │    │
│  │ • FareCalculatorService (✅ City-based) │    │
│  │ • RouteEstimationService (✅ Fallback)  │    │
│  │ • PromoCodeService                      │    │
│  │ • RideNotificationService               │    │
│  └─────────────────────────────────────────┘    │
└────────────────┬─────────────────────────────────┘
                 │
        ┌────────┼────────┬──────────┐
        ↓        ↓        ↓          ↓
      PostgreSQL  Redis  Google  SignalR
      Database   Cache   Maps    Hubs
```

---

## 🧪 Testing Checklist

- [ ] ✅ User sipariş verir → Driver'lara notification gidiyor
- [ ] ✅ Driver qebul ediyor → User'da doğru polyline gösterilir
- [ ] ✅ Polyline haritada düzgün render ediliyor (tuhaf çizgi yok)
- [ ] ✅ Distance ve Duration gösterilir (null değil)
- [ ] ✅ User iptal ediyor → Driver'ın notification'ı silinir
- [ ] ✅ Promo code trip history'de discount gösterilir
- [ ] ✅ Şəhir-spesifik tariff uygulanır
- [ ] ✅ SignalR reconnection çalışır
- [ ] ✅ Google Maps API başarısız olsa bile fallback çalışır

---

## 🐛 Troubleshooting

### Polyline Tuhaf Görünüyor

**Çözüm:**
1. Backend logs'u kontrol et: `"Route estimation returned no valid data"`
2. Google Maps API key'i kontrol et
3. Fallback polyline oluşturuluyor (çalışıyor olmalı)

```javascript
// Frontend'de polyline decode'unu kontrol et
const decoded = decodePolyline(encodedPolyline);
console.log('Decoded coords:', decoded); // Min 2 nokta olmalı
```

---

### Driver'ın Notification'ı Silinmiyor

**Çözüm:**
1. Driver `"all_drivers"` grup'unda mı? (DriverHub.OnConnectedAsync'ı kontrol et)
2. Frontend event listener'ı var mı?

```javascript
connection.on('RideStatusUpdated', (rideId, status, payload) => {
  if (payload.Status === 'Canceled') {
    // Remove from list
  }
});
```

---

### Distance/Duration Null

**Çözüm:**
1. Backend'de fallback calculation kontrol et
2. Logs'u kontrol et: `"using fallback calculation"`
3. Frontend'de null check yapılmalı:

```javascript
const distance = payload.distanceKm ?? 0;
const duration = payload.durationMinutes ?? 0;
```

---

### Promo Code Discount Gösterilmiyor

**Çözüm:**
1. User'ın aktif promo code'u var mı? (Database'i kontrol et)
2. PromoCodeService.ApplyAsync başarılı mı?
3. Trip history endpoint'i promo code hesaplıyor mu?

---

## 📞 Support & Contact

**Backend Issues:**
- Check logs: `dotnet run --configuration Debug`
- Repository: https://github.com/deepcode201/TurboTaxi

**Frontend Issues:**
- Enable React Native debug mode
- Check console logs in Flipper/Android Studio

---

## 📄 Change Log

### Version 1.0 (2025-02-26)

**✅ Fixed:**
- Polyline fallback system (Google Maps fail-safe)
- User cancel → Driver notification removal
- Distance & Duration always populated
- City-based tariff system
- Promo code trip history discount

**🆕 Added:**
- Haversine distance calculation
- Google Maps polyline encoding
- Fallback polyline generation
- Trip history with discount fields

**🔄 Updated:**
- RideService with fallback handlers
- DriverHub with auto-subscribe to "all_drivers"
- FareCalculatorService with city parameter
- TripHistoryModels with discount fields

---

## 📦 Dependencies

```
Backend:
- .NET 10
- Entity Framework Core
- PostgreSQL
- Redis
- SignalR
- Google Maps API

Frontend:
- React Native
- @microsoft/signalr
- react-native-maps
- axios / fetch API
```

---

## 📝 License

MIT License - See LICENSE file

---

## 👥 Contributors

**Backend:** TurboTaxi Development Team
**Frontend Support:** React Native Integration Guide

---

**Last Updated:** 2025-02-26  
**Status:** ✅ All Issues Resolved  
**Version:** 1.0 (Production Ready)
