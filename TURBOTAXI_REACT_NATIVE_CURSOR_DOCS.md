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

export interface CreateRideResponse {
    rideId: number;
    status: RideStatus;
    candidateDriverIds: number[];
    estimatedFare: number; // Estimated fare in AZN (e.g., 25.50)
    distanceKm: number; // Distance in kilometers
    durationMinutes: number; // Estimated trip duration
}

// SignalR Payloads
export interface NewRideRequestPayload {
    rideId: number;
    vehicleType: string; // Vehicle category (Eco, Comfort, Premium)
    pickupLat: number;
    pickupLng: number;
    destinationLat: number;
    destinationLng: number;
    encodedPolyline: string; // Google Maps encoded polyline
    estimatedFare: number; // Driver's potential earnings
    distanceKm: number; // Distance in kilometers
    durationMinutes: number; // Estimated trip duration
}

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

export interface RideCompletionPayload {
    rideId: number;
    status: "Completed"; // RideStatus.Completed
    finalPrice: number; // Final fare amount in currency
    completedAtUtc: string; // ISO 8601 datetime
    clearMap: boolean; // Always true for completion
    message?: string; // User-friendly message (e.g., "Trip completed! Total: 25.50 AZN")
    userType?: "user" | "driver"; // Indicates which app received the notification
}
```

---

## 🌐 3. REST API: Ride Creation & App Startup / Crash Recovery

### A. Create Ride Request
**Endpoint:** `POST /api/rides` (Header: `Authorization: Bearer <JWT>`)

**Request Body:**
```json
{
  "pickupLat": 40.3869,
  "pickupLng": 49.8671,
  "destinationLat": 40.4093,
  "destinationLng": 49.9214,
  "vehicleType": "Eco",
  "userId": 1
}
```

**Response (200 OK):**
```json
{
  "rideId": 123,
  "status": "Pending",
  "candidateDriverIds": [5, 10, 15],
  "estimatedFare": 25.50,
  "distanceKm": 12.5,
  "durationMinutes": 18
}
```

**Action (User App):**
1. Display the estimated fare, distance, and duration on the UI before confirmation.
2. Show this info: `"Estimated fare: 25.50 AZN | Distance: 12.5 km | Duration: ~18 min"`
3. Store the `rideId` in app state.
4. Connect to SignalR immediately to listen for `RideStatusUpdated` (when driver accepts).

---

### B. App Startup / Crash Recovery

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

#### Event 0: `NewRideRequest` (Driver App Only) ⭐ **NEW**
- Triggered when a user creates a new ride and nearby drivers are available.
- **Payload:** `NewRideRequestPayload`
```typescript
{
  rideId: 123;
  vehicleType: "Eco";
  pickupLat: 40.3869;
  pickupLng: 49.8671;
  destinationLat: 40.4093;
  destinationLng: 49.9214;
  encodedPolyline: "..."; // Google Maps encoded polyline
  estimatedFare: 25.50; // Driver's potential earnings
  distanceKm: 12.5;
  durationMinutes: 18;
}
```
- **Action (Driver App):**
  1. Display ride notification with key info: **Fare**, **Distance**, **Duration**, **Vehicle Category**.
  2. Example UI: `"🚗 Eco Ride | Earn 25.50 AZN | 12.5 km | ~18 min"`.
  3. Parse `encodedPolyline` and show route preview on map.
  4. Present "Accept" / "Decline" buttons.
  5. On Accept → Call `POST /api/rides/{rideId}/accept`.

---

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
- **Payload shapes depend on the event trigger:**

##### Sub-case 3a: Ride Completion (`status: "Completed"`)
Cast to `RideCompletionPayload`:
```typescript
{
  rideId: number;
  status: "Completed";
  finalPrice: number;        // e.g., 25.50
  completedAtUtc: string;    // ISO 8601 timestamp
  clearMap: boolean;         // Always true
  message?: string;          // e.g., "Trip completed! Total: 25.50 AZN. Thank you!"
  userType?: "user" | "driver"; // Lets client customize UI
}
```
- **Action (User App):**
  1. Display final amount on a summary screen.
  2. Show message: `${finalPrice} AZN`.
  3. Clear the map (hide markers, polyline).
  4. Trigger optional payment confirmation or rating screen.

- **Action (Driver App):**
  1. Display earnings: `"You earned ${finalPrice} AZN"`.
  2. Update driver balance/earnings history.
  3. Clear the map.
  4. Transition to "Available for new rides" state.

##### Sub-case 3b: Other Status Changes (`status: "Pending" | "Approved" | "OnWay" | "Cancelled"`)
- **Payload:** Generic `{ rideId: number, status: string, ...otherFields? }`
- **Action:** Update UI elements (e.g., change bottom sheet text from "Driver arriving" to "Ride in progress"). If `Cancelled`, clear active map state.

---

## 🚦 5. High-Level Flow summary for Cursor AI:
1. Initialize the app. Verify Auth.
2. Call `GET /api/rides/active`. Hydrate state.
3. Start SignalR Connection.
4. **If USER:** Mount listeners for `DriverLocationUpdated`, `RouteUpdate`, `RideStatusUpdated`. Watch the car move smoothly.
5. **If DRIVER:** Mount listeners for `RouteUpdate`, `RideStatusUpdated`. Start a GPS watch that invokes `UpdateLocation` to the SignalR hub every 3-5s. Follow the drawn line.

> **Final note for Cursor:** Focus strictly on performance. Minimizing re-rendering components when `DriverLocationUpdated` fires is crucial for keeping JS thread frame rates high. Extract the map marker and polyline into memoized/isolated components if necessary.