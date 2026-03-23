# TurboTaxi — SignalR Real-Time Documentation
> React Native Developer Guide  \
> Branch: `SignalRModify` | Base URL: `https://YOUR_SERVER/hubs/driver`

---

## 🔍 Hub Overview

**Hub:** `DriverHub` (`/hubs/driver`)

**Server methods (client → server):**
- `SubscribeUser(userId: string)` – join `user:{userId}` group
- `UnsubscribeUser(userId: string)` – leave `user:{userId}` group
- `SubscribeDriver(driverId: string)` – join `driver:{driverId}` group
- `UnsubscribeDriver(driverId: string)` – leave `driver:{driverId}` group
- `UpdateLocation(request: DriverLocationUpdateRequest)` – primary real-time driver GPS update

**Client methods (server → client):**
- `DriverLocationUpdated(driverId: number, lat: number, lng: number)`
- `NewRideRequest(requestId: string, payload: object)`
- `RideStatusUpdated(rideId: number, status: string, payload: object)`

**`DriverLocationUpdateRequest` shape (SignalR + REST):**

```json
{
  "driverId": 3,
  "lat": 40.3810,
  "lng": 49.8911,
  "vehicleType": 0,      // 0=Eco, 1=Business, 2=XL, 3=Delivery
  "statusOptional": 1    // 0=Offline, 1=Free, 2=Busy, 3=BusySoon
}
```

---

## 📦 Installation (React Native)

```bash
npm install @microsoft/signalr
```

---

## 🔌 Connection Setup

```javascript
import * as signalR from '@microsoft/signalr';

const connection = new signalR.HubConnectionBuilder()
  .withUrl('https://YOUR_SERVER/hubs/driver', {
    skipNegotiation: false,
    transport: signalR.HttpTransportType.WebSockets,
  })
  .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
  .configureLogging(signalR.LogLevel.Information)
  .build();

// Start connection
await connection.start();
console.log('SignalR Connected:', connection.connectionId);
```

---

## 👥 Group Subscription

After connecting, **immediately** subscribe to the correct group depending on user role.

### 👤 User (Passenger)
```javascript
// Subscribe — call once after connection starts
await connection.invoke('SubscribeUser', String(userId));

// Unsubscribe — call on logout / component unmount
await connection.invoke('UnsubscribeUser', String(userId));
```

### 🚗 Driver
```javascript
// Subscribe — call once after connection starts
await connection.invoke('SubscribeDriver', String(driverId));

// Unsubscribe — call on logout / component unmount
await connection.invoke('UnsubscribeDriver', String(driverId));
```

> ⚠️ `userId` and `driverId` must be passed as **string**, even if they are integers.

---

## 📡 Server → Client Events

Register these listeners **before** calling `connection.start()`.

---

### 1. `DriverLocationUpdated`
Real-time driver GPS location. Fires on every driver location update.

**Received by:** User (during active ride) + Driver (own location echo)

```javascript
connection.on('DriverLocationUpdated', (driverId, lat, lng) => {
  console.log(`Driver #${driverId} → lat: ${lat}, lng: ${lng}`);

  // Update map marker
  setDriverLocation({ latitude: lat, longitude: lng });
});
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `driverId` | `number` | Driver ID |
| `lat` | `number` | Latitude |
| `lng` | `number` | Longitude |

---

### 2. `NewRideRequest`
New ride request available for nearby driver to accept.

**Received by:** Driver only

```javascript
connection.on('NewRideRequest', (requestId, payload) => {
  console.log('New ride request:', payload);
  /*
  payload = {
    rideId: 12,
    pickupLat: 40.3777,
    pickupLng: 49.8920,
    destinationLat: 40.4093,
    destinationLng: 49.8671,
    vehicleType: "Eco",
    encodedPolyline: "abc123...",   // Google Maps encoded polyline (nullable)
    distanceKm: 4.2,                // nullable
    durationMinutes: 11             // nullable
  }
  */

  // Show ride request popup to driver
  showRideRequestModal(payload);
});
```

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `rideId` | `number` | ❌ | Ride ID |
| `pickupLat` | `number` | ❌ | Pickup latitude |
| `pickupLng` | `number` | ❌ | Pickup longitude |
| `destinationLat` | `number` | ✅ | Destination latitude |
| `destinationLng` | `number` | ✅ | Destination longitude |
| `vehicleType` | `string` | ❌ | `Eco` / `Business` / `XL` / `Delivery` |
| `encodedPolyline` | `string` | ✅ | Google encoded polyline |
| `distanceKm` | `number` | ✅ | Estimated distance |
| `durationMinutes` | `number` | ✅ | Estimated duration |

---

### 3. `RideStatusUpdated`
All ride lifecycle events come through this single event. Use the `status` field to determine what happened.

**Received by:** User or Driver depending on `status`

```javascript
connection.on('RideStatusUpdated', (rideId, status, payload) => {
  console.log(`Ride #${rideId} → status: ${status}`, payload);

  switch (status) {
    // ─── USER receives ───────────────────────────────
    case 'ride_update':       handleRideCreated(payload);      break;
    case 'Approved':          handleDriverAccepted(payload);   break;
    case 'OnWay':             handleTripStarted(payload);      break;
    case 'Completed':         handleTripCompleted(payload);    break;

    // ─── DRIVER receives ─────────────────────────────
    case 'notification':      handleDriverNotified(payload);   break;
    case 'CanceledByUser':    handleUserCanceled(payload);     break;

    // ─── ALL DRIVERS receive ─────────────────────────
    case 'RideCanceled':      handleRideCanceled(payload);     break;
    case 'RideTaken':         handleRideTakenByOther(payload); break;
  }
});
```

---

## 🔄 Status Payload Reference

---

### `ride_update` — Ride Created (→ User)
Fired immediately after user creates a ride. Drivers are being searched.

```json
{
  "rideId": 12,
  "status": "Pending",
  "pickupLat": 40.3777,
  "pickupLng": 49.8920,
  "destinationLat": 40.4093,
  "destinationLng": 49.8671,
  "encodedPolyline": "abc123...",
  "distanceKm": 4.2,
  "durationMinutes": 11,
  "message": "Searching for nearby drivers..."
}
```

---

### `Approved` — Driver Accepted (→ User)
A driver accepted the ride.

```json
{
  "rideId": 12,
  "status": "Approved",
  "driverId": 3,
  "driverLat": 40.3810,
  "driverLng": 49.8911,
  "pickupLat": 40.3777,
  "pickupLng": 49.8920,
  "destinationLat": 40.4093,
  "destinationLng": 49.8671,
  "encodedPolyline": "abc123...",
  "distanceKm": 1.2,
  "durationMinutes": 4,
  "message": "Driver is coming! ETA: 4 min"
}
```

> `driverLat` / `driverLng` can be `null` if driver location not available yet.

---

### `notification` — Ride Accepted Echo (→ Driver)
Driver's own confirmation after accepting a ride.

```json
{
  "rideId": 12,
  "status": "Approved",
  "pickupLat": 40.3777,
  "pickupLng": 49.8920,
  "destinationLat": 40.4093,
  "destinationLng": 49.8671,
  "encodedPolyline": "abc123...",
  "distanceKm": 1.2,
  "durationMinutes": 4,
  "message": "Navigate to pickup location"
}
```

---

### `OnWay` — Trip Started (→ User)
Driver picked up the passenger, trip is in progress.

```json
{
  "rideId": 12,
  "status": "OnWay",
  "startedAt": "2025-02-25T14:30:00Z",
  "driverLat": 40.3777,
  "driverLng": 49.8920,
  "destinationLat": 40.4093,
  "destinationLng": 49.8671,
  "encodedPolyline": "xyz456...",
  "distanceKm": 4.2,
  "durationMinutes": 11,
  "message": "Trip started! ETA: 11 min"
}
```

---

### `Completed` — Trip Finished (→ User & Driver)
Ride is completed and fare is confirmed.

**User payload:**
```json
{
  "rideId": 12,
  "status": "Completed",
  "finalPrice": 4.80,
  "completedAt": "2025-02-25T14:45:00Z",
  "clearMap": true,
  "encodedPolyline": null,
  "message": "Trip completed! Total: 4.80 AZN. Thank you!"
}
```

**Driver payload:**
```json
{
  "rideId": 12,
  "status": "Completed",
  "finalPrice": 4.80,
  "completedAt": "2025-02-25T14:45:00Z",
  "clearMap": true,
  "encodedPolyline": null,
  "message": "Trip completed! You earned 4.80 AZN"
}
```

> When `clearMap: true` → clear polyline and markers from map.

---

### `CanceledByUser` — User Canceled (→ Driver)
User canceled the ride while driver was assigned.

```json
{
  "rideId": 12,
  "status": "CanceledByUser",
  "clearMap": true,
  "message": "User canceled this ride"
}
```

---

### `RideCanceled` — Ride No Longer Available (→ All Drivers)
Broadcast to all drivers when a pending ride is canceled before any driver accepted.

```json
{
  "rideId": 12,
  "status": "Canceled",
  "message": "This ride has been canceled by the user"
}
```

---

### `RideTaken` — Ride Accepted by Someone Else (→ All Drivers)
Another driver already accepted this ride.

```json
{
  "rideId": 12,
  "status": "Taken",
  "acceptedByDriverId": 5,
  "message": "This ride was accepted by another driver"
}
```

---

## 🗺️ Encoded Polyline

`encodedPolyline` is a **Google Maps Encoded Polyline** string. Decode it in React Native:

```bash
npm install @mapbox/polyline
```

```javascript
import polyline from '@mapbox/polyline';

const coordinates = polyline.decode(encodedPolyline).map(([lat, lng]) => ({
  latitude: lat,
  longitude: lng,
}));

// Use with react-native-maps Polyline
<Polyline coordinates={coordinates} strokeWidth={4} strokeColor="#FF6B00" />
```

---

## 📍 Driver Location Update (REST)

Driver sends location via **HTTP POST** (not SignalR). The server automatically broadcasts it to the user via `DriverLocationUpdated`.

```
POST /api/DriverLocation/update
Content-Type: application/json

{
  "driverId": 3,
  "lat": 40.3810,
  "lng": 49.8911,
  "vehicleType": "Eco",     // 0=Eco, 1=Business, 2=XL, 3=Delivery
  "statusOptional": "Busy"  // "Free" | "Busy"
}
```

> Recommended interval: every **2–3 seconds** while ride is active.

---

## 🔁 Full Connection Lifecycle Example

```javascript
import * as signalR from '@microsoft/signalr';

class TurboTaxiSignalR {
  constructor() {
    this.connection = null;
  }

  async connect(userId) {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('https://YOUR_SERVER/hubs/driver')
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();

    // Register all listeners BEFORE start
    this.registerListeners();

    await this.connection.start();
    await this.connection.invoke('SubscribeUser', String(userId));

    console.log('Connected & subscribed as user:', userId);
  }

  registerListeners() {
    this.connection.on('DriverLocationUpdated', (driverId, lat, lng) => {
      // update map marker
    });

    this.connection.on('NewRideRequest', (requestId, payload) => {
      // show request popup (driver only)
    });

    this.connection.on('RideStatusUpdated', (rideId, status, payload) => {
      switch (status) {
        case 'ride_update':   /* searching drivers */ break;
        case 'Approved':      /* driver on the way */ break;
        case 'OnWay':         /* trip in progress  */ break;
        case 'Completed':     /* trip done         */ break;
        case 'CanceledByUser':/* ride canceled     */ break;
        case 'RideTaken':     /* taken by other    */ break;
        case 'RideCanceled':  /* no longer avail.  */ break;
      }
    });

    this.connection.onreconnecting(() => console.warn('Reconnecting...'));
    this.connection.onreconnected(() => console.log('Reconnected'));
    this.connection.onclose(() => console.error('Connection closed'));
  }

  async disconnect(userId) {
    await this.connection?.invoke('UnsubscribeUser', String(userId));
    await this.connection?.stop();
  }
}

export default new TurboTaxiSignalR();
```

---

## 🚦 Ride Status Flow

```
[User]                              [Driver]
  │                                    │
  │── POST /api/rides ─────────────────┤
  │◄── RideStatusUpdated               │
  │    status: "ride_update"           │◄── NewRideRequest
  │    "Searching drivers..."          │    (new ride available)
  │                                    │
  │                         POST /api/rides/{id}/accept
  │◄── RideStatusUpdated               │◄── RideStatusUpdated
  │    status: "Approved"              │    status: "notification"
  │    "Driver is coming!"             │    "Navigate to pickup"
  │                                    │
  │◄── DriverLocationUpdated (every 2-3s)
  │    live driver position on map     │
  │                                    │
  │                         POST /api/rides/{id}/start
  │◄── RideStatusUpdated               │◄── RideStatusUpdated
  │    status: "OnWay"                 │    status: "OnWay"
  │    "Trip started!"                 │
  │                                    │
  │◄── DriverLocationUpdated (every 2-3s)
  │                                    │
  │                         POST /api/rides/{id}/finish
  │◄── RideStatusUpdated               │◄── RideStatusUpdated
  │    status: "Completed"             │    status: "Completed"
  │    "Total: 4.80 AZN"              │    "Earned 4.80 AZN"
```

---

## ⚙️ VehicleType Enum

| ID | Name |
|----|------|
| `0` | `Eco` |
| `1` | `Business` |
| `2` | `XL` |
| `3` | `Delivery` |

---

## ❌ Error Handling

```javascript
try {
  await connection.invoke('SubscribeUser', String(userId));
} catch (err) {
  console.error('SignalR invoke failed:', err);
}

// Global connection error
connection.onclose((error) => {
  if (error) {
    console.error('Connection closed with error:', error);
    // Retry logic or show reconnecting UI
  }
});
```

---

*Last updated: TurboTaxi API — branch `SignalRModify`*
