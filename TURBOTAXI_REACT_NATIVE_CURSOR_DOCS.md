# 📱 TurboTaxi - React Native Integration Document for Cursor AI 🤖

> **Context for Cursor AI:** You are a senior React Native developer. Your task is to implement the real-time taxi tracking and ride state management for the "TurboTaxi" mobile app. You will handle both the **User App** (Passenger) and **Driver App** experiences using REST APIs for state hydration and **SignalR WebSockets** for real-time location and route updates. Please strictly follow the endpoints, models, and logic flow below.

## ⚙️ 1. Architecture & Protocol Requirements
- **Authentication:** All requests (REST and SignalR) must include the JWT token in headers (`Authorization: Bearer <token>`).
- **Real-time Protocol:** Microsoft SignalR (`@microsoft/signalr` package is recommended).
- **Map Visuals:** `react-native-maps` should be used. Smooth animation for driver movement is required. Do NOT call Google Maps Directions API from the app directly during an active ride (Backend handles this to save costs).

---

## 🏗️ 2. Domain Models (TypeScript Interfaces)

```typescript
// Shared Types
export enum RideStatus {
    Pending = "Pending",
    Approved = "Approved", // Driver accepted, going to pickup user
    OnWay = "OnWay",       // Driver picked up user, going to destination
    Completed = "Completed",
    Cancelled = "Cancelled"
}

export interface Coordinates {
    latitude: number;
    longitude: number;
}

// REST Responses
export interface ActiveRideResponse {
    rideId: number;
    status: RideStatus;
    pickup: Coordinates;
    destination: Coordinates;
    currentDriverLocation: Coordinates;
    routePolyline: string; // Encoded Google Maps Polyline
    distanceKm: number;
    durationMinutes: number;
}

// SignalR Payloads
export interface RouteUpdatePayload {
    rideId: number;
    encodedPolyline: string;
    distanceKm: number;
    durationMinutes: number;
    status: string; // RideStatus
    pickupLat: number;
    pickupLng: number;
    destinationLat: number;
    destinationLng: number;
    isRouteUpdate: boolean; // Always true for this event
}

export interface DriverLocationPayload {
    driverId: number;
    latitude: number;
    longitude: number;
    heading?: number;
}
```

---

## 🌐 3. REST API: App Startup / Crash Recovery

If the app reloads, crashes, or opens from a background state, the client must NOT depend solely on SignalR to catch up. It must execute a state hydration request.

**Endpoint:** `GET /api/rides/active` (Header: `Authorization: Bearer <JWT>`)

**Logic for Cursor AI:**
1. On App Mount (or Map Screen mount), call `GET /api/rides/active`.
2. If `200 OK` is returned, populate the active ride state, render the `pickup` and `destination` markers, draw the `routePolyline`, and set the initial `currentDriverLocation` marker.
3. Then, connect to the SignalR Hub.

---

## 📡 4. SignalR WebSockets Implementation

### A. Connection
- **User Hub URL:** `BASE_URL/hubs/user` (or `/hubs/ride` depending on backend config)
- **Driver Hub URL:** `BASE_URL/hubs/driver`
- Connect using `withUrl(url, { accessTokenFactory: () => jwtToken })`
- Ensure auto-reconnect is enabled.

### B. Driver App Payload (Emitting to Server)
Drivers must send their live GPS location **every 3 to 5 seconds** while the ride is active (`Approved` or `OnWay`).

- **Invoke Method:** `UpdateLocation`
- **Data Shape:** `{ latitude: number, longitude: number }`
- **Cursor Instruction:** Use `react-native-geolocation-service`. Do not use HTTP POST for ongoing tracking; broadcast strictly via the active SignalR connection.

### C. Listening to Events (Server -> Client)

#### Event 1: `DriverLocationUpdated` (User App Only)
- Triggered when the driver emits their location.
- **Action:** Animate the car marker on the map to the new `{ latitude, longitude }`. Do NOT redraw the route polyline.

#### Event 2: `RouteUpdate` (Both User & Driver Apps)
- **Trigger:** Handled by backend `OffRouteDetectionService`. If the backend detects the driver is >150 meters away from the line, it recalculates the Google Maps off-route polyline and sends it down.
- **Action:** 
  1. Cast payload to `RouteUpdatePayload`.
  2. Parse `encodedPolyline` (using `@mapbox/polyline` or custom decoder).
  3. Replace the old route on the Map.
  4. Update the UI estimators: `DistanceKm` and `DurationMinutes`.
  5. **IMPORTANT:** Do not move the initial Pickup or Destination static map markers, only update the active drawing line.

#### Event 3: `RideStatusUpdated` (Both Apps)
- Triggered when state changes (e.g., Driver arrives at pickup, ride starts, ride finishes).
- **Payload:** `{ rideId: number, status: string }`
- **Action:** Update UI elements (e.g., change bottom sheet text from "Driver arriving" to "Ride in progress"). If `Completed`, clear active map state.

---

## 🚦 5. High-Level Flow summary for Cursor AI:
1. Initialize the app. Verify Auth.
2. Call `GET /api/rides/active`. Hydrate state.
3. Start SignalR Connection.
4. **If USER:** Mount listeners for `DriverLocationUpdated`, `RouteUpdate`, `RideStatusUpdated`. Watch the car move smoothly.
5. **If DRIVER:** Mount listeners for `RouteUpdate`, `RideStatusUpdated`. Start a GPS watch that invokes `UpdateLocation` to the SignalR hub every 3-5s. Follow the drawn line.

> **Final note for Cursor:** Focus strictly on performance. Minimizing re-rendering components when `DriverLocationUpdated` fires is crucial for keeping JS thread frame rates high. Extract the map marker and polyline into memoized/isolated components if necessary.