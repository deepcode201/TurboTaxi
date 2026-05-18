# TurboTaxi - Backend Düzeltmeleri Dokümantasyonu

## 📱 React Native Developer İçin Backend Integrasyon Kılavuzu

---

## 1️⃣ **Polyline Sorunu - ÇÖZÜLDÜ ✅**

### Problem
User sipariş verip Driver qebul ettiğinde, User'ın haritasında polyline **tuhaf bir çizgi** olarak görünüyordu.

```
BEFORE (❌):
┌─────────────────┐
│  Tuhaf Çizgi    │  ← Google Maps API başarısız
│  (düz çizgi)    │
└─────────────────┘
```

### Çözüm
Backend'de **fallback polyline** oluşturuldu. Google Maps başarısız olsa bile, Driver-Pickup arasında bir polyline gönderiliyor.

### React Native Tarafında Yapılması Gerekenler

#### 1. **Polyline Komponent'i**

```javascript
import MapView, { Polyline } from 'react-native-maps';

const RideMapScreen = ({ encodedPolyline, driverLocation, pickupLocation, destinationLocation }) => {
  const [polylineCoords, setPolylineCoords] = useState([]);

  // Polyline'ı decode et
  useEffect(() => {
    if (encodedPolyline) {
      const decoded = decodePolyline(encodedPolyline);
      setPolylineCoords(decoded);
    }
  }, [encodedPolyline]);

  return (
    <MapView
      style={{ flex: 1 }}
      initialRegion={{
        latitude: driverLocation?.lat || pickupLocation?.lat,
        longitude: driverLocation?.lng || pickupLocation?.lng,
        latitudeDelta: 0.0922,
        longitudeDelta: 0.0421,
      }}
    >
      {/* Polyline çizgisini göster */}
      {polylineCoords.length > 0 && (
        <Polyline
          coordinates={polylineCoords}
          strokeColor="#FF6B6B"
          strokeWidth={3}
        />
      )}

      {/* Driver Marker */}
      {driverLocation && (
        <MapView.Marker
          coordinate={{
            latitude: driverLocation.lat,
            longitude: driverLocation.lng,
          }}
          title="Driver"
          pinColor="blue"
        />
      )}

      {/* Pickup Marker */}
      {pickupLocation && (
        <MapView.Marker
          coordinate={{
            latitude: pickupLocation.lat,
            longitude: pickupLocation.lng,
          }}
          title="Pickup"
          pinColor="green"
        />
      )}

      {/* Destination Marker */}
      {destinationLocation && (
        <MapView.Marker
          coordinate={{
            latitude: destinationLocation.lat,
            longitude: destinationLocation.lng,
          }}
          title="Destination"
          pinColor="red"
        />
      )}
    </MapView>
  );
};

// Polyline Decoder Utility
const decodePolyline = (encoded) => {
  const poly = [];
  let index = 0,
    lat = 0,
    lng = 0;

  while (index < encoded.length) {
    let result = 0;
    let shift = 0;
    let b;

    do {
      b = encoded.charCodeAt(index++) - 63;
      result |= (b & 0x1f) << shift;
      shift += 5;
    } while (b >= 0x20);

    const dlat = result & 1 ? ~(result >> 1) : result >> 1;
    lat += dlat;

    result = 0;
    shift = 0;

    do {
      b = encoded.charCodeAt(index++) - 63;
      result |= (b & 0x1f) << shift;
      shift += 5;
    } while (b >= 0x20);

    const dlng = result & 1 ? ~(result >> 1) : result >> 1;
    lng += dlng;

    poly.push({
      latitude: lat / 1e5,
      longitude: lng / 1e5,
    });
  }

  return poly;
};

export default RideMapScreen;
```

#### 2. **Backend'den Gelen Polyline Verisi**

```javascript
// SignalR dari Backend'den gelen veri
connection.on('RideStatusUpdated', (rideId, status, payload) => {
  // payload şu bilgileri içerir:
  const {
    RideId,
    Status,              // "Approved", "OnWay", "Completed", "Canceled"
    EncodedPolyline,     // ← Google Maps encoded polyline (YENI!)
    DistanceKm,          // ← Mesafe
    DurationMinutes,     // ← Beklenen sürü
    DriverLat,           // ← Driver'ın latitude'ü
    DriverLng,           // ← Driver'ın longitude'ü
    PickupLat,
    PickupLng,
    DestinationLat,
    DestinationLng,
    Message,
    FinalPrice,
  } = payload;

  // Harita'ya polyline render et
  if (EncodedPolyline) {
    const decoded = decodePolyline(EncodedPolyline);
    displayPolylineOnMap(decoded);
  }
});
```

---

## 2️⃣ **User Cancel Sorunu - ÇÖZÜLDÜ ✅**

### Problem
User sipariş iptal ettiğinde, Driver'ın notification'ı silinmiyor.

```
BEFORE (❌):
User: "Sipariş iptal ettim" → Backend ✓ → Driver: "Hala notification var" ❌
```

### Çözüm
Tüm Driver'lar bağlandığı zaman **"all_drivers"** grup'una auto-subscribe oluyor.

```javascript
// Backend
// DriverHub.cs - OnConnectedAsync()
public override async Task OnConnectedAsync()
{
    // Driver otomatik olarak "all_drivers" grubuna ekleniyor
    await Groups.AddToGroupAsync(Context.ConnectionId, "all_drivers");
    await base.OnConnectedAsync();
}
```

### React Native Tarafında Yapılması Gerekenler

#### 1. **Notification Listener**

```javascript
import { useEffect, useState } from 'react';
import { HubConnectionBuilder } from '@microsoft/signalr';

const RideListScreen = () => {
  const [activeRides, setActiveRides] = useState([]); // [{ rideId, status, ... }]
  const [connection, setConnection] = useState(null);

  useEffect(() => {
    // SignalR bağlantısı kur
    const hubConnection = new HubConnectionBuilder()
      .withUrl('https://yourapi.com/driver-hub')
      .withAutomaticReconnect()
      .build();

    hubConnection.start();

    // ✅ NEW: RideStatusUpdated event'ini dinle
    hubConnection.on('RideStatusUpdated', (rideId, status, payload) => {
      console.log('Ride Status Updated:', rideId, payload.Status);

      // Eğer sipariş "Canceled" ise list'ten kaldır
      if (payload.Status === 'Canceled') {
        setActiveRides((prev) =>
          prev.filter((ride) => ride.rideId !== rideId)
        );
        
        // Toast notification göster
        showToast('Sipariş iptal edildi', 'info');
      }

      // Eğer sipariş "Taken" ise list'ten kaldır
      if (payload.Status === 'Taken') {
        setActiveRides((prev) =>
          prev.filter((ride) => ride.rideId !== rideId)
        );
      }
    });

    // ✅ NEW: NewRideRequest - Yeni sipariş geldi
    hubConnection.on('NewRideRequest', (requestId, payload) => {
      console.log('New Ride Request:', payload.RideId);

      // Yeni sipariş'i list'e ekle
      const newRide = {
        rideId: payload.RideId,
        pickupLat: payload.PickupLat,
        pickupLng: payload.PickupLng,
        destLat: payload.DestinationLat,
        destLng: payload.DestinationLng,
        estimatedFare: payload.EstimatedFare,
        distanceKm: payload.DistanceKm,        // ← YENI!
        durationMinutes: payload.DurationMinutes, // ← YENI!
        vehicleType: payload.VehicleType,
        encodedPolyline: payload.EncodedPolyline, // ← YENI!
      };

      setActiveRides((prev) => [newRide, ...prev]);
    });

    setConnection(hubConnection);

    return () => {
      hubConnection.stop();
    };
  }, []);

  return (
    <FlatList
      data={activeRides}
      keyExtractor={(item) => item.rideId.toString()}
      renderItem={({ item }) => (
        <RideCard
          rideId={item.rideId}
          distance={item.distanceKm}      // ← YENI!
          duration={item.durationMinutes}  // ← YENI!
          fare={item.estimatedFare}
          polyline={item.encodedPolyline}  // ← YENI!
          onAccept={() => acceptRide(item.rideId)}
          onDecline={() => declineRide(item.rideId)}
        />
      )}
    />
  );
};

export default RideListScreen;
```

#### 2. **Ride Card Komponenti**

```javascript
const RideCard = ({
  rideId,
  distance,      // ← YENI!
  duration,      // ← YENI!
  fare,
  polyline,      // ← YENI!
  onAccept,
  onDecline,
}) => {
  return (
    <Card style={styles.card}>
      <View style={styles.header}>
        <Text style={styles.distance}>{distance?.toFixed(1)} km</Text>
        <Text style={styles.duration}>{duration} min</Text>
      </View>

      {/* Mini Harita Preview */}
      {polyline && (
        <View style={styles.mapPreview}>
          <MiniMapPreview
            encodedPolyline={polyline}
            height={100}
          />
        </View>
      )}

      <View style={styles.fareSection}>
        <Text style={styles.fareLabel}>Ödeme:</Text>
        <Text style={styles.fareAmount}>{fare} AZN</Text>
      </View>

      <View style={styles.buttons}>
        <Button
          title="Reddət"
          color="#FF6B6B"
          onPress={onDecline}
        />
        <Button
          title="Qebul Et"
          color="#51CF66"
          onPress={onAccept}
        />
      </View>
    </Card>
  );
};

export default RideCard;
```

---

## 3️⃣ **Mesafe ve Müddet - ÇÖZÜLDÜ ✅**

### Problem
Driver'a Order type çıkıyor ama **DistanceKm** ve **DurationMinutes** null oluyordu.

### Çözüm
Backend'de **fallback calculation** eklendi. Google Maps başarısız olsa bile, Haversine formülü ile mesafe/müddet hesaplanıyor.

### Backend Endpoint'leri

```javascript
// 1. Yeni Sipariş Oluştur
POST /api/rides/create
{
  "userId": 1,
  "pickupLat": 40.4093,
  "pickupLng": 49.8599,
  "destinationLat": 40.3856,
  "destinationLng": 49.8989,
  "vehicleType": "Eco"  // "Eco", "Comfort", "Premium"
}

Response:
{
  "rideId": 123,
  "status": "Pending",
  "estimatedFare": 5.50,
  "distanceKm": 6.25,        // ← YENI!
  "durationMinutes": 12       // ← YENI!
}

// 2. Trip History - Promo Code ile
GET /api/trip-history/user/{userId}?pageNumber=1&pageSize=20

Response:
{
  "items": [
    {
      "rideId": 123,
      "fare": 5.50,          // ← Orijinal fiyat
      "discountAmount": 0.55, // ← YENI! Promo code discount
      "finalFare": 4.95,     // ← YENI! İndirimli fiyat
      "status": "Completed",
      "driverName": "Hasan",
      "distance": "6.25 km",
      "duration": "12 min"
    }
  ]
}
```

---

## 4️⃣ **Şəhər Əsasında Qiymətlər - ÇÖZÜLDÜ ✅**

### Tariff Sistemi

```javascript
// Bakı üçün Tariffler (Backend'de):
{
  "city": "Baku",
  "vehicleType": "Eco",
  "baseFare": 2.0,           // Açılış ücreti
  "pricePerKm": 0.30,        // km başına
  "pricePerMinute": 0.15,    // dakika başına
  "minimumFare": 5.0         // Minimum ödeme
}
```

### Fare Hesablanması

```javascript
// Frontend'de
const calculateFare = (distanceKm, durationMinutes) => {
  const baseFare = 2.0;
  const pricePerKm = 0.30;
  const pricePerMinute = 0.15;
  const minimumFare = 5.0;

  let fare = baseFare;
  fare += distanceKm * pricePerKm;
  fare += durationMinutes * pricePerMinute;
  
  return Math.max(fare, minimumFare);
};

// Promo Code ile indirim
const applyPromoCode = (originalFare, promoCode) => {
  let discount = 0;
  
  if (promoCode.discountType === 'Percentage') {
    discount = originalFare * (promoCode.discountValue / 100);
    if (promoCode.maxDiscountAmount) {
      discount = Math.min(discount, promoCode.maxDiscountAmount);
    }
  } else {
    discount = promoCode.discountValue; // Fixed Amount
  }

  return {
    discountAmount: discount,
    finalFare: Math.max(0, originalFare - discount)
  };
};
```

---

## 5️⃣ **Promo Code Sistemi - ÇÖZÜLDÜ ✅**

### Backend Endpoints

```javascript
// 1. Promo Code Oluştur
POST /api/promo-codes/create
{
  "userId": 1,
  "isPercentage": true,
  "number": 10,             // %10 indirim
  "expireRideCount": 5      // 5 seferlik
}

// 2. Promo Code Uygula
POST /api/promo-codes/apply
{
  "userId": 1,
  "promoCodeId": 1,
  "originalFare": 10.00
}

Response:
{
  "discountAmount": 1.00,   // %10 indirim
  "finalFare": 9.00
}

// 3. User Trip History (Discount ile)
GET /api/trip-history/user/{userId}
```

### React Native Promo Code UI

```javascript
const PromoCodeScreen = () => {
  const [promoCode, setPromoCode] = useState('');
  const [discountInfo, setDiscountInfo] = useState(null);

  const applyPromo = async () => {
    try {
      const response = await fetch('https://api.turbotaxi.com/promo-codes/apply', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          userId: currentUserId,
          promoCodeId: promoCodeId,
          originalFare: 10.00
        })
      });

      const data = await response.json();
      setDiscountInfo(data); // { discountAmount, finalFare }

      showToast(`${data.discountAmount} AZN indirim yapıldı!`, 'success');
    } catch (error) {
      showToast('Promo code geçersiz', 'error');
    }
  };

  return (
    <View style={styles.container}>
      <TextInput
        style={styles.input}
        placeholder="Promo code gir"
        value={promoCode}
        onChangeText={setPromoCode}
      />

      <Button
        title="Uygula"
        onPress={applyPromo}
      />

      {discountInfo && (
        <View style={styles.discountBox}>
          <Text style={styles.discountLabel}>Orijinal Fiyat: {discountInfo.originalFare} AZN</Text>
          <Text style={styles.discount}>İndirim: -{discountInfo.discountAmount} AZN</Text>
          <Text style={styles.finalPrice}>Son Fiyat: {discountInfo.finalFare} AZN</Text>
        </View>
      )}
    </View>
  );
};
```

---

## 6️⃣ **Backend API Endpoints Özeti**

### User Endpoints

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| POST | `/api/rides/create` | Yeni sipariş oluştur |
| GET | `/api/trip-history/user/{userId}` | User'ın seyahat geçmişi |
| POST | `/api/promo-codes/apply` | Promo code uygula |

### Driver Endpoints

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| POST | `/api/rides/{rideId}/accept` | Sipariş qebul et |
| POST | `/api/rides/{rideId}/start` | Seyahate başla |
| POST | `/api/rides/{rideId}/finish` | Seyahati bitir |
| GET | `/api/trip-history/driver/{driverId}` | Driver'ın seyahat geçmişi |

---

## 7️⃣ **SignalR Events Özeti**

### Driver'a Gelen Events

```javascript
// Yeni sipariş notifikasyonu
connection.on('NewRideRequest', (requestId, payload) => {
  // payload: {
  //   rideId, vt vehicleType, pickupLat, pickupLng,
  //   destinationLat, destinationLng, encodedPolyline,
  //   estimatedFare, distanceKm, durationMinutes
  // }
});

// Sipariş durumu güncellendi
connection.on('RideStatusUpdated', (rideId, status, payload) => {
  // payload: { Status: "Approved", "OnWay", "Completed", "Canceled" }
});
```

### User'a Gelen Events

```javascript
// Sipariş durumu güncellendi
connection.on('RideStatusUpdated', (rideId, status, payload) => {
  // payload: { status, driverId, driverLat, driverLng, ... }
});
```

---

## 8️⃣ **Environment Variables (.env)**

```bash
# Backend URL
API_BASE_URL=https://yourapi.turbotaxi.com
SIGNALR_URL=https://yourapi.turbotaxi.com/driver-hub

# Google Maps API
GOOGLE_MAPS_API_KEY=your_api_key

# API Keys
DRIVER_API_KEY=your_driver_key
USER_API_KEY=your_user_key
```

---

## 9️⃣ **Hata Yönetimi**

```javascript
// Backend'den gelen hata responses
const handleApiError = (error) => {
  const { status, data } = error.response;

  if (status === 400) {
    // Bad Request
    showToast(data.error || 'Geçersiz istek', 'error');
  } else if (status === 404) {
    // Not Found
    showToast('Sipariş bulunamadı', 'error');
  } else if (status === 500) {
    // Server Error
    showToast('Sunucu hatası, lütfen tekrar deneyin', 'error');
  }
};

// SignalR connection errors
connection.onclose(async () => {
  console.log('SignalR disconnected');
  showToast('Bağlantı koptu', 'warning');
  
  // Reconnect
  setTimeout(() => {
    connection.start();
  }, 5000);
});
```

---

## 🔟 **Testing Checklist**

- [ ] ✅ Polyline haritada doğru gösteriliyor
- [ ] ✅ Distance ve Duration gösteriliyor
- [ ] ✅ User cancel ettiğinde Driver notification silinir
- [ ] ✅ Promo code discount doğru hesaplanıyor
- [ ] ✅ SignalR reconnection çalışıyor
- [ ] ✅ Offline mode'da notification queue'de bekliyor
- [ ] ✅ Trip history Promo code discount gösteriyor

---

## 📞 **Support**

Backend'de sorun varsa, logs'u kontrol et:
```bash
# Server logs
dotnet run --verbose
```

Frontend'de sorun varsa:
```javascript
// Debug logging
console.log('Event received:', data);
connection.on('RideStatusUpdated', (...args) => {
  console.log('Args:', args);
});
```

---

**Last Updated:** 2025-02-26  
**Version:** 1.0
