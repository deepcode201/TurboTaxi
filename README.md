# TurboTaxi React Native Client Integration Guide

## Project Overview

TurboTaxi is a mobile ride-hailing solution. The React Native client interacts with a .NET REST API and a SignalR real-time backend to:
- Create ride requests
- Accept, start, finish, and cancel rides
- Receive real-time updates such as driver location, ride status changes, and new ride requests

Backend stack:
- .NET API controllers for REST endpoints
- SignalR hub for real-time communication

## Environments & Base URLs

Known environments:
- Development
  - Base API URL: http://localhost:<port>
  - SignalR hub URL: http://localhost:<port>/hubs/driver
  - In development, Swagger is available at /
- Production (as seen in `wwwroot/production-test.html`)
  - Base API URL: http://booktables-001-site1.anytempurl.com
  - SignalR hub URL: http://booktables-001-site1.anytempurl.com/hubs/driver

Notes:
- SignalR hub is mapped at path `/hubs/driver` (`Program.cs` ? `app.MapHub<DriverHub>("/hubs/driver")`).
- CORS allows all origins in production and specific localhost origins in development.

## Authentication

Current backend configuration shows `app.UseAuthorization()` but no explicit authentication middleware; the documented endpoints appear to be accessible without a token. If authentication is later added (e.g., JWT bearer):
- Obtain a token via the provided auth endpoint.
- Send `Authorization: Bearer <token>` in requests and use `accessTokenFactory` for SignalR.

React Native axios example with an optional token header:

```ts
import axios from "axios";

const api = axios.create({
  baseURL: "https://api.example.com", // replace with actual environment URL
});

export async function postWithAuth<T>(path: string, payload: unknown, token?: string): Promise<T> {
  const res = await api.post<T>(path, payload, {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
  });
  return res.data;
}
```

## REST API Endpoints

Base controller: `RidesController` (`[Route("api/[controller]")]` ? `/api/rides`)

### Group: Rides

#### POST /api/rides
Creates a new ride request.

Request body (inferred from `RideService`):
```json
{
  "userId": 123,
  "pickupLat": 40.409,
  "pickupLng": 49.867,
  "destinationLat": 40.420,
  "destinationLng": 49.880,
  "vehicleType": "Standard"
}
```

- Path params: none
- Query params: none
- Description: Creates a ride, estimates route, notifies nearby drivers and the user.
- Success response (inferred):
```json
{
  "rideId": 101,
  "status": "Pending",
  "candidateDriverIds": [5, 8, 12]
}
```
- Common errors:
  - 400: Invalid state/parameters
  - 404: Related entities not found
  - 500: Failed to create ride

Example (HTTP):
```
POST /api/rides HTTP/1.1
Content-Type: application/json

{
  "userId": 123,
  "pickupLat": 40.409,
  "pickupLng": 49.867,
  "destinationLat": 40.420,
  "destinationLng": 49.880,
  "vehicleType": "Standard"
}
```

React Native (axios):
```ts
import axios from "axios";

const api = axios.create({ baseURL: "<BASE_API_URL>" });

export type CreateRideRequest = {
  userId: number;
  pickupLat: number;
  pickupLng: number;
  destinationLat?: number;
  destinationLng?: number;
  vehicleType: string; // Standard | Comfort | Business | Van
};

export async function createRide(payload: CreateRideRequest) {
  const { data } = await api.post("/api/rides", payload);
  return data;
}
```

#### POST /api/rides/{rideId}/accept
Accepts a pending ride by a driver.

Request body (inferred):
```json
{
  "driverId": 5
}
```

- Path params:
  - rideId: number
- Description: Validates driver existence and ride state, assigns driver, updates Redis status, notifies user/driver/others.
- Success response (inferred):
```json
{
  "rideId": 101,
  "driverId": 5,
  "status": "Approved"
}
```
- Common errors:
  - 404: Ride or driver not found
  - 400: Invalid ride state or driver mismatch
  - 500: Unexpected error

Example (HTTP):
```
POST /api/rides/101/accept HTTP/1.1
Content-Type: application/json

{
  "driverId": 5
}
```

React Native (fetch):
```ts
export async function acceptRide(rideId: number, driverId: number) {
  const res = await fetch(`<BASE_API_URL>/api/rides/${rideId}/accept`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ driverId }),
  });
  if (!res.ok) throw new Error(await res.text());
  return res.json();
}
```

#### POST /api/rides/{rideId}/start
Marks the accepted ride as started.

Request body (inferred):
```json
{
  "driverId": 5
}
```

- Path params:
  - rideId: number
- Description: Validates approved state and driver, updates status to `OnWay`, estimates route to destination, notifies user/driver.
- Success response (inferred):
```json
{
  "rideId": 101,
  "status": "OnWay",
  "startedAtUtc": "2025-12-07T10:00:00Z"
}
```
- Common errors:
  - 404: Ride not found
  - 400: Invalid state or driver mismatch
  - 500: Unexpected error

React Native (axios):
```ts
export async function startRide(rideId: number, driverId: number) {
  const { data } = await api.post(`/api/rides/${rideId}/start`, { driverId });
  return data;
}
```

#### POST /api/rides/{rideId}/finish
Completes the ride and sets final price.

Request body (inferred):
```json
{
  "driverId": 5,
  "finalPrice": 15.75
}
```

- Path params:
  - rideId: number
- Description: Validates `OnWay` state and driver, updates status to `Completed`, clears Redis driver ride fields, notifies user/driver.
- Success response (inferred):
```json
{
  "rideId": 101,
  "status": "Completed",
  "finalPrice": 15.75,
  "finishedAtUtc": "2025-12-07T10:25:00Z"
}
```
- Common errors:
  - 404: Ride not found
  - 400: Invalid state or driver mismatch
  - 500: Unexpected error

React Native (fetch):
```ts
export async function finishRide(rideId: number, driverId: number, finalPrice: number) {
  const res = await fetch(`<BASE_API_URL>/api/rides/${rideId}/finish`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ driverId, finalPrice }),
  });
  if (!res.ok) throw new Error(await res.text());
  return res.json();
}
```

#### POST /api/rides/{rideId}/cancel-by-user
Cancels a ride by the user.

Request body (inferred):
```json
{
  "userId": 123
}
```

- Path params:
  - rideId: number
- Description: Validates user, cancels ride, frees driver if assigned, may notify all drivers if previously pending.
- Success response (inferred):
```json
{
  "rideId": 101,
  "status": "Canceled"
}
```
- Common errors:
  - 404: Ride not found
  - 400: User mismatch or cannot cancel completed ride

React Native (axios):
```ts
export async function cancelRideByUser(rideId: number, userId: number) {
  const { data } = await api.post(`/api/rides/${rideId}/cancel-by-user`, { userId });
  return data;
}
```

#### POST /api/rides/{rideId}/cancel-by-driver
Cancels a ride by the driver.

Request body (inferred):
```json
{
  "driverId": 5
}
```

- Path params:
  - rideId: number
- Description: Validates driver, cancels ride, frees driver in Redis, notifies user.
- Success response (inferred):
```json
{
  "rideId": 101,
  "status": "Canceled"
}
```
- Common errors:
  - 404: Ride not found
  - 400: Driver mismatch or cannot cancel completed ride

React Native (fetch):
```ts
export async function cancelRideByDriver(rideId: number, driverId: number) {
  const res = await fetch(`<BASE_API_URL>/api/rides/${rideId}/cancel-by-driver`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ driverId }),
  });
  if (!res.ok) throw new Error(await res.text());
  return res.json();
}
```

## Real-Time Communication (SignalR Hubs & Channels)

Hub: `DriverHub` (`TurboTaxi.Realtime.Hubs.DriverHub`)
- URL: `<BASE_API_URL>/hubs/driver`
- Purpose:
  - Manage subscriptions for drivers and users using SignalR groups
  - Send real-time notifications:
    - Driver location updates
    - New ride requests
    - Ride status updates

Client-to-server methods (invoked by React Native):
- `SubscribeDriver(driverId: string)`
- `UnsubscribeDriver(driverId: string)`
- `SubscribeUser(userId: string)`
- `UnsubscribeUser(userId: string)`

Server-to-client methods (handlers you must register with `connection.on`):
- `DriverLocationUpdated(driverId: number, lat: number, lng: number)`
- `NewRideRequest(requestId: string, payload: any)`
- `RideStatusUpdated(rideId: number, status: string, payload: any)`

Groups:
- `driver:{driverId}`
- `user:{userId}`
Join/leave via `SubscribeDriver`, `UnsubscribeDriver`, `SubscribeUser`, `UnsubscribeUser`.

React Native SignalR example (TypeScript):
```ts
import * as SignalR from "@microsoft/signalr";

const hubUrl = "<BASE_API_URL>/hubs/driver";

export function createDriverHubConnection(token?: string) {
  const connection = new SignalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: token ? () => token : undefined,
    })
    .withAutomaticReconnect()
    .configureLogging(SignalR.LogLevel.Information)
    .build();

  // Server-to-client handlers
  connection.on("DriverLocationUpdated", (driverId: number, lat: number, lng: number) => {
    console.log("DriverLocationUpdated:", { driverId, lat, lng });
  });

  connection.on("NewRideRequest", (requestId: string, payload: any) => {
    console.log("NewRideRequest:", { requestId, payload });
  });

  connection.on("RideStatusUpdated", (rideId: number, status: string, payload: any) => {
    console.log("RideStatusUpdated:", { rideId, status, payload });
  });

  async function start() {
    try {
      if (connection.state === SignalR.HubConnectionState.Disconnected) {
        await connection.start();
        console.log("SignalR connected");
      }
    } catch (err) {
      console.error("SignalR start error", err);
      // Exponential backoff retry
      setTimeout(start, 2000);
    }
  }

  async function subscribeDriver(driverId: number) {
    await connection.invoke("SubscribeDriver", String(driverId));
  }

  async function unsubscribeDriver(driverId: number) {
    await connection.invoke("UnsubscribeDriver", String(driverId));
  }

  async function subscribeUser(userId: number) {
    await connection.invoke("SubscribeUser", String(userId));
  }

  async function unsubscribeUser(userId: number) {
    await connection.invoke("UnsubscribeUser", String(userId));
  }

  async function stop() {
    try {
      await connection.stop();
    } catch (err) {
      console.error("SignalR stop error", err);
    }
  }

  // Reconnect events
  connection.onreconnecting((err) => console.warn("Reconnecting...", err));
  connection.onreconnected((id) => console.log("Reconnected. ConnectionId:", id));
  connection.onclose((err) => console.warn("Disconnected", err));

  return { connection, start, stop, subscribeDriver, unsubscribeDriver, subscribeUser, unsubscribeUser };
}
```

## End-to-end Example Flows

### Create a ride and listen for status updates (User)
1. REST: `POST /api/rides` with user’s pickup/destination and vehicle type.
2. SignalR:
   - Connect to hub and `SubscribeUser(<userId>)`.
   - Listen for:
     - `RideStatusUpdated` for status changes (Pending ? Approved ? OnWay ? Completed or Canceled).
     - `NewRideRequest` if relevant to user flows (initial feedback).
3. React Native:
```ts
const { connection, start, subscribeUser } = createDriverHubConnection(/* optional token */);

await start();
await subscribeUser(userId);

connection.on("RideStatusUpdated", (rideId: number, status: string, payload: any) => {
  // Update UI based on status: Approved (driver assigned), OnWay (trip started), Completed, Canceled
});

const ride = await createRide({
  userId,
  pickupLat,
  pickupLng,
  destinationLat,
  destinationLng,
  vehicleType: "Standard",
});
```

### Driver accepts and starts a ride; user tracks driver in real-time
1. Driver REST:
   - `POST /api/rides/{rideId}/accept` with `driverId`.
   - `POST /api/rides/{rideId}/start` with `driverId`.
2. User SignalR:
   - `SubscribeUser(<userId>)`
   - Listen to `RideStatusUpdated` for `Approved` and `OnWay`.
   - Optionally listen to `DriverLocationUpdated` for live map updates if emitted in your flow.
3. React Native (driver side):
```ts
await acceptRide(rideId, driverId);
await startRide(rideId, driverId);
```

### Finish ride and notify both parties
1. Driver REST:
   - `POST /api/rides/{rideId}/finish` with `driverId` and `finalPrice`.
2. SignalR:
   - Both `user:{userId}` and `driver:{driverId}` groups receive `RideStatusUpdated` with `Completed` plus final price details.
3. React Native:
```ts
await finishRide(rideId, driverId, finalPrice);
```

### Cancel flows
- User cancels: `POST /api/rides/{rideId}/cancel-by-user` with `userId`
- Driver cancels: `POST /api/rides/{rideId}/cancel-by-driver` with `driverId`
- SignalR: A `RideStatusUpdated` event is sent with `Canceled` and contextual payload.

## Error Handling & Troubleshooting

REST:
- 400 Bad Request: Invalid state transitions (e.g., starting a ride that isn’t approved), mismatched user/driver IDs.
- 404 Not Found: Ride or driver doesn’t exist.
- 500 Internal Server Error: Unexpected server failures or database issues.

SignalR:
- Connection issues:
  - Use `.withAutomaticReconnect()` and implement handlers (`onreconnecting`, `onreconnected`, `onclose`).
  - Retry `connection.start()` on failure with backoff.
- Authorization errors:
  - If auth is added later, ensure `accessTokenFactory` returns a valid token and refresh if expired.

Recommendations:
- Handle expired tokens by:
  - Refreshing and retrying REST calls.
  - Recreating the SignalR connection with a fresh token.
- Lost SignalR connection:
  - Maintain local state; resume subscriptions (`SubscribeUser`/`SubscribeDriver`) after reconnection.
- Timeouts:
  - Add client-side timeouts and user feedback.
  - Re-attempt critical actions with controlled retries.

## Security & Secrets

- Do not hardcode real secrets or API keys in the app.
- Use environment files (e.g., `.env`) and secure storage for tokens.
- For React Native:
  - Store auth tokens in secure storage (e.g., Expo SecureStore or react-native-keychain).
- Replace any secret values with placeholders like `<YOUR_API_KEY_HERE>`.

## Appendix: Quick Reference

- Base API endpoints:
  - POST `/api/rides`
  - POST `/api/rides/{rideId}/accept`
  - POST `/api/rides/{rideId}/start`
  - POST `/api/rides/{rideId}/finish`
  - POST `/api/rides/{rideId}/cancel-by-user`
  - POST `/api/rides/{rideId}/cancel-by-driver`
- SignalR hub:
  - URL: `<BASE_API_URL>/hubs/driver`
  - Client-to-server:
    - `SubscribeDriver(string driverId)`
    - `UnsubscribeDriver(string driverId)`
    - `SubscribeUser(string userId)`
    - `UnsubscribeUser(string userId)`
  - Server-to-client:
    - `DriverLocationUpdated(int driverId, double lat, double lng)`
    - `NewRideRequest(string requestId, object payload)`
    - `RideStatusUpdated(int rideId, string status, object payload)`
