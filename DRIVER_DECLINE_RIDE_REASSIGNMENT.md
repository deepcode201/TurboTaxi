# 🔄 Driver Ride Decline & Auto-Reassignment System

## Overview
When a driver declines/cancels an accepted ride, instead of completely canceling the ride, the system now **automatically resets the ride to "Pending" status and reassigns it to other available drivers**. The ride remains active until the user explicitly cancels it.

## Business Logic Flow

### Before (Old Behavior)
```
Driver Accepts Ride → Driver Cancels → Ride Status = "Canceled" ❌
User sees ride canceled and cannot request another driver
```

### After (New Behavior)
```
Driver Accepts Ride → Driver Declines → Ride Resets to "Pending" 
→ Automatically sent to other available drivers ✅
User sees message "Searching for another driver..."
```

## Implementation Details

### Modified Method
**File:** `TurboTaxi.Infrastructure\Services\RideService.cs`  
**Method:** `CancelRideByDriverAsync(int rideId, CancelRideByDriverRequest request)`

### Step-by-Step Process

#### 1. **Free the Driver**
```csharp
// Driver status updated to "Free" in Redis
// Remove driver from current ride tracking
ride.DriverId = null;
```

#### 2. **Reset Ride to Pending**
```csharp
ride.Status = RideStatus.Pending;
ride.DriverId = null;
ride.AcceptedTime = null;
ride.PickUpTime = null;
ride.RequestedTime = DateTime.UtcNow; // Fresh timestamp for new search
await _db.SaveChangesAsync(ct);
```

#### 3. **Re-estimate Route**
```csharp
var (polyline, distanceKm, durationMin) = await EstimateRouteAsync(
    ride.StartLocation.Latitude, ride.StartLocation.Longitude,
    ride.EndLocation.Latitude, ride.EndLocation.Longitude,
    ct);
```

#### 4. **Find Nearby Drivers (Excluding the Declining Driver)**
```csharp
var nearbyDrivers = await _redis.GeoRadiusAsync(
    $"geo:drivers:{ride.RequestedVehicleType.ToString().ToLowerInvariant()}",
    ride.StartLocation.Longitude,
    ride.StartLocation.Latitude,
    3.0); // 3km radius

// Skip the driver who just declined
foreach (var r in nearbyDrivers)
{
    var did = int.Parse(r.Member);
    if (did == request.DriverId) continue; // ⏩ Skip declining driver
    
    // Check if driver is Free and recently active
    if (driverStatus == "Free" && isRecent)
    {
        candidateIds.Add(did);
    }
}
```

#### 5. **Send NewRideRequest to Available Drivers**
```csharp
if (candidateIds.Count > 0)
{
    var driverPayload = new NewRideRequestPayload
    {
        RideId = ride.Id,
        VehicleType = ride.RequestedVehicleType.ToString(),
        PickupLat = ride.StartLocation.Latitude,
        PickupLng = ride.StartLocation.Longitude,
        DestinationLat = ride.EndLocation.Latitude,
        DestinationLng = ride.EndLocation.Longitude,
        EncodedPolyline = polyline,
        EstimatedFare = estimatedFare,
        DistanceKm = distanceKm,
        DurationMinutes = durationMin
    };
    
    await _notification.NotifyNearestDriversAsync(candidateIds, driverPayload);
}
```

#### 6. **Notify User of Reassignment**
```csharp
var userPayload = new RideCompletionPayload
{
    RideId = ride.Id,
    Status = "Pending",
    ClearMap = false,
    Message = "Driver not available. Searching for another driver..."
};
await _notification.NotifyUserAsync(ride.UserId, userPayload);
```

## API Response

### Before Reassignment Attempt
```json
POST /api/rides/{rideId}/cancel/driver
{
  "driverId": 3
}
```

### After Successful Reassignment
```json
{
  "rideId": 123,
  "status": "Pending"  // Changed from "Canceled" to "Pending"
}
```

### Failure Scenario (No Available Drivers)
```json
{
  "rideId": 123,
  "status": "Pending",
  "message": "Driver declined. No other drivers available currently."
}
```

## User-Side Notifications (React Native)

### Notification Events

#### Event 1: Driver Declined
```javascript
userConnection.on('RideStatusUpdated', (rideId, status, payload) => {
  if (payload.status === 'Pending' && payload.message.includes('Searching')) {
    showNotification("Driver not available. Searching for another driver...");
    disableAcceptButton();
    showLoadingSpinner();
  }
});
```

#### Event 2: New Driver Accepted
```javascript
userConnection.on('RideStatusUpdated', (rideId, status, payload) => {
  if (payload.status === 'Accepted') {
    showNotification("New driver accepted your ride!");
    hideLoadingSpinner();
    updateDriverInfo(payload.driverId, payload.driverLocation);
  }
});
```

#### Event 3: No Available Drivers
```javascript
userConnection.on('RideStatusUpdated', (rideId, status, payload) => {
  if (payload.status === 'Pending' && payload.message.includes('No other drivers')) {
    showAlert('No drivers available. Please try again in a moment.');
    enableAcceptButton(); // Allow user to manually cancel
  }
});
```

## Database State Changes

### Ride Entity State Transitions

| State | Before | After |
|-------|--------|-------|
| **Status** | Accepted | Pending |
| **DriverId** | 3 (driver who declined) | null |
| **AcceptedTime** | 2024-01-20 15:30:00 | null |
| **PickUpTime** | null | null |
| **RequestedTime** | 2024-01-20 15:25:00 | 2024-01-20 15:35:00 (updated) |

### Redis State Changes

#### Driver Hash (driver:3)
```
Before:
- status: Busy
- currentRideId: 123
- currentUserId: 1

After:
- status: Free
- currentRideId: (deleted)
- currentUserId: (deleted)
```

#### Geo-spatial Data
```
Automatically reindexed for new driver search
geo:drivers:eco → finds all available Eco drivers within 3km
```

## Error Handling

### Validation Checks
```csharp
// Cannot decline if ride already completed
if (ride.Status == RideStatus.Completed)
    throw new InvalidOperationException("Cannot cancel completed ride");

// Cannot decline if ride already canceled
if (ride.Status == RideStatus.Canceled)
    throw new InvalidOperationException("Ride already canceled");

// Driver mismatch check
if (ride.DriverId != request.DriverId)
    throw new InvalidOperationException("Driver mismatch");
```

### Reassignment Failure Fallback
If no drivers are available after reassignment attempt:
1. Ride remains in "Pending" status
2. User is notified: "No drivers available. Please try again in a moment."
3. User can manually cancel the ride at any time
4. Ride can be searched again with a new CreateRide request (or use same ride ID if supported)

## Logging & Debugging

### Log Entries for Successful Reassignment
```
[INFO] Driver #3 declining ride #123
[INFO] Driver #3 freed
[INFO] Ride #123 reset to Pending status for reassignment
[INFO] Ride #123 reset to Pending
[INFO] Estimating route...
[INFO] Found 5 drivers for reassignment
[INFO] Skipping driver #3 (declined this ride)
[INFO] Driver #2 is available for reassignment
[INFO] Driver #4 is available for reassignment
[INFO] Driver #5 is available for reassignment
[INFO] Notifying 3 drivers about reassigned ride
[INFO] Ride #123 reassigned to 3 drivers
[INFO] User #1 notified about driver reassignment
```

### Log Entries for Failed Reassignment
```
[INFO] Driver #3 declining ride #123
[INFO] Driver #3 freed
[WARN] No available drivers found for reassignment of ride #123
[INFO] User #1 notified: "No drivers available"
```

## Testing Guide

### Test Scenario 1: Successful Reassignment

**Setup:**
- User requests a ride
- Driver #3 accepts the ride
- Drivers #2, #4, #5 are nearby and Free

**Actions:**
1. Driver #3 calls POST `/api/rides/123/cancel/driver`
2. Wait for notifications

**Expected Results:**
- ✅ Ride status changes to Pending in DB
- ✅ Driver #3 freed in Redis
- ✅ Drivers #2, #4, #5 receive NewRideRequest
- ✅ User receives "Searching for another driver..." message
- ✅ Another driver (e.g., #2) accepts the ride
- ✅ Ride moves to Accepted status

### Test Scenario 2: No Available Drivers

**Setup:**
- User requests a ride
- Driver #3 accepts the ride
- No other drivers are nearby/free

**Actions:**
1. Driver #3 calls POST `/api/rides/123/cancel/driver`
2. Wait for notifications

**Expected Results:**
- ✅ Ride resets to Pending
- ✅ Driver #3 freed
- ✅ No NewRideRequest sent to other drivers
- ✅ User receives "No drivers available" message
- ✅ User can manually cancel the ride

### Test Scenario 3: User Cancels While Reassigning

**Setup:**
- User requests a ride
- Driver #3 accepts
- Driver #3 declines (reassignment starts)
- Before any other driver accepts, user cancels

**Actions:**
1. Driver #3 cancels → Ride becomes Pending
2. User sees loading and calls POST `/api/rides/123/cancel/user`

**Expected Results:**
- ✅ Ride transitions to Canceled
- ✅ Any NewRideRequests to other drivers are ignored (ride no longer pending)
- ✅ Ride cannot be reassigned again

## Performance Considerations

### Redis Geo-radius Query
- **Radius:** 3km (configurable)
- **Time:** ~50-100ms for typical city with 1000 drivers
- **Optimization:** Filter active drivers first (within last 60 seconds)

### Database Updates
- **Transactions:** Uses SaveChangesAsync (atomic)
- **Queries:** Eager loads StartLocation and EndLocation
- **Indices:** Ensure `Rides.Status` and `Rides.DriverId` are indexed

### SignalR Broadcast
- **Drivers Notified:** Only available drivers within radius
- **Latency:** ~100-200ms for real-time notification delivery
- **Fallback:** If notification fails, ride remains searchable

## Configuration & Customization

### Adjustable Parameters

```csharp
// In CancelRideByDriverAsync method

// Search radius (kilometers)
const double SEARCH_RADIUS_KM = 3.0;

// Driver activity cutoff (seconds)
const int DRIVER_ACTIVITY_CUTOFF_SECONDS = 60;

// Retry mechanism (future enhancement)
const int MAX_REASSIGNMENT_ATTEMPTS = 2;
```

## Future Enhancements

1. **Retry Mechanism:** After X seconds, if no driver accepts, auto-search again
2. **Expanding Radius:** Start at 3km, expand to 5km, then 10km if no drivers found
3. **Priority Queue:** Prefer drivers closer to pickup location
4. **Driver Rating Filter:** Only offer to drivers with rating > 4.0
5. **Promo Code Transfer:** Maintain user's promo code when reassigning

## Related Files

- `TurboTaxi.Infrastructure\Services\RideService.cs` - Core implementation
- `TurboTaxi.Realtime\Services\RideNotificationService.cs` - Driver notifications
- `TurboTaxi.Realtime\Hubs\UserHub.cs` - User event delivery
- `TurboTaxi.Domain\Entities\Ride.cs` - Ride entity definition
- `TurboTaxi.Models\Rides\RideModels.cs` - API request/response DTOs

## Summary

The driver ride decline system now provides a seamless experience where:
- Users don't lose their ride request if a driver declines
- Ride automatically searches for other available drivers
- System is transparent—users are notified at each step
- All changes are persisted to database and Redis
- Comprehensive logging for debugging and monitoring
