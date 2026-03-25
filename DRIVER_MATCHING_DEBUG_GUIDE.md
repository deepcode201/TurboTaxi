# 🔍 Driver Matching Debugging Guide

## Issue
Riders create rides, but `candidateDriverIds` is always empty despite online drivers sending location.

## Debug Endpoints Available

### 1. **Get All Drivers in System**
```bash
GET /api/debug/driver-matching/drivers/{vehicleType}
```
**Parameters:**
- `vehicleType`: 0=Eco, 1=Business, 2=XL, 3=Delivery

**Response:**
Shows all drivers in the geo-spatial index with their complete Redis data (status, location, updatedAt).

**Example:**
```bash
curl http://localhost:5000/api/debug/driver-matching/drivers/0
```

---

### 2. **Test Driver Matching Logic**
```bash
POST /api/debug/driver-matching/match
```
**Request Body:**
```json
{
  "lat": 40.41,
  "lng": 49.88,
  "radiusKm": 3.0,
  "vehicleType": 0
}
```

**Response Shows:**
- How many drivers found in GeoRadiusAsync
- Why each driver was ACCEPTED or REJECTED
- Specific rejection reasons (status not "Free", stale location, etc.)

**Example:**
```bash
curl -X POST http://localhost:5000/api/debug/driver-matching/match \
  -H "Content-Type: application/json" \
  -d '{"lat": 40.41, "lng": 49.88, "radiusKm": 3.0, "vehicleType": 0}'
```

---

### 3. **Get Single Driver Details**
```bash
GET /api/debug/driver-matching/driver/{driverId}
```
**Shows:**
- Driver's complete Redis hash data
- Which geo-spatial indices they're in
- Exact field values causing rejection

**Example:**
```bash
curl http://localhost:5000/api/debug/driver-matching/driver/3
```

---

## Debugging Steps

### Step 1: Verify Drivers Are Online
```bash
GET /api/debug/driver-matching/drivers/0
```

**Expected Output:**
- Multiple drivers with status="Free"
- Recent updatedAt timestamps (within last 60 seconds)
- Correct lat/lng values (not 0,0)

**If Empty:**
- ❌ No drivers in geo-spatial index
- Check: Is DriverLocationService.UpdateAsync() being called?
- Check: Are drivers connected to SignalR? (see driver logs)
- Check: Are they sending `UpdateLocation` via SignalR?

---

### Step 2: Test Geo Query at Ride Pickup Location
```bash
POST /api/debug/driver-matching/match
{
  "lat": <RIDER_PICKUP_LAT>,
  "lng": <RIDER_PICKUP_LNG>,
  "radiusKm": 3.0,
  "vehicleType": 0
}
```

**Expected Output:**
- `geoRadiusFound` > 0 (drivers within 3km)
- `matched.count` should be same or close to `geoRadiusFound`
- If matched.count = 0, check `rejected[].reason` for why drivers were rejected

**If geoRadiusFound = 0:**
- ❌ GeoRadiusAsync returned no drivers
- Possible causes:
  1. Geo-spatial index not populated (check Step 1)
  2. Rider location is > 3km from all drivers
  3. Radius is too small (try 10km)

**If geoRadiusFound > 0 but matched.count = 0:**
- ❌ All matching drivers were rejected
- Check rejection reasons:
  - "Status is 'X', not 'Free'" → Driver status not being set correctly
  - "Stale (updatedAt ≤ cutoff)" → Driver location not recent
  - "Invalid updatedAt format" → DateTime format issue

---

### Step 3: Check Specific Driver
```bash
GET /api/debug/driver-matching/driver/3
```

**Expected Output:**
```json
{
  "driverId": 3,
  "redisHash": {
    "status": "Free",
    "lat": "40.41",
    "lng": "49.88",
    "vehicleType": "Eco",
    "updatedAt": "2024-12-19T10:30:45.1234567Z"
  },
  "geoIndices": {
    "Eco": { "lat": 40.41, "lng": 49.88 }
  }
}
```

**Issues to Look For:**
- ❌ status is "1" instead of "Free" → Enum conversion issue
- ❌ lat/lng are "0.0" → Bad coordinates being sent
- ❌ updatedAt is missing → Not being stored
- ❌ updatedAt format is wrong → Can't parse as DateTime
- ❌ Geo index doesn't contain driver → GeoAddAsync not working

---

## Common Issues & Fixes

### Issue 1: Status Stored as "1" Instead of "Free"
**Root Cause:** Enum conversion in DriverLocationService
**Fix:** Ensure DriverLocationService.UpdateAsync() calls .ToString() on DriverStatus enum
**Verification:** Check driver details, status should be "Free", "Offline", "Busy", or "BusySoon"

---

### Issue 2: GeoRadiusAsync Returns 0 Drivers
**Root Cause:** Geo-spatial index not being populated
**Fix:** Verify GeoAddAsync is called in DriverLocationService.UpdateAsync()
**Verification:**
1. Driver sends location
2. Check server logs: "✓ Driver #X geo-spatial index updated"
3. Run GET /api/debug/driver-matching/drivers/0 → should see drivers

---

### Issue 3: All Drivers Rejected as "Stale"
**Root Cause:** updatedAt timestamp is too old (> 60 seconds)
**Fix:** Drivers should send UpdateLocation at least once per 60 seconds
**Verification:**
1. Run GET /api/debug/driver-matching/driver/3
2. Check updatedAt timestamp - should be within last minute
3. If old, driver hasn't sent location in 60+ seconds

---

### Issue 4: Drivers Match But NewRideRequest Not Sent
**This means driver matching is WORKING**
**Root Cause:** Issue with SignalR notification sending
**Check:**
1. Are drivers actually connected to `/hubs/driver`? (check driver logs)
2. Are they subscribed? (called `SubscribeDriver`)
3. Check RideNotificationService logs for errors
4. Verify RideService.NotifyNearestDriversAsync() is being called

---

## Testing Workflow

### Manual Testing

**1. Start Server**
```bash
cd TurboTaxi.API
dotnet run
```

**2. Get Drivers (using Postman/curl)**
```bash
GET http://localhost:5000/api/debug/driver-matching/drivers/0
```
Verify drivers exist and have `status="Free"` and recent `updatedAt`.

**3. Create Ride (using Rides API)**
```bash
POST http://localhost:5000/api/rides/create
{
  "userId": 10,
  "vehicleType": 0,
  "pickupLat": <NEAR_A_DRIVER>,
  "pickupLng": <NEAR_A_DRIVER>,
  "destinationLat": 40.5,
  "destinationLng": 49.9
}
```

**4. Check Server Logs**
Look for these log entries:
- `?? Searching for nearby drivers...`
- `   Found X drivers within 3km`
- `   Driver #Y data: status='Free', updatedAt='...'`
- `   ✓ Driver #Y is AVAILABLE`
- `?? Notifying X available drivers`

**5. Test Match Logic Directly**
```bash
POST http://localhost:5000/api/debug/driver-matching/match
{
  "lat": 40.41,
  "lng": 49.88,
  "radiusKm": 3.0,
  "vehicleType": 0
}
```

If this returns `matched.count = 0`, check the `rejected[]` array for why drivers don't match.

---

## Logs to Monitor

**DriverLocationService:**
```
✓ Driver #3 hash updated in Redis: driver:3
✓ Driver #3 geo-spatial index updated: geo:drivers:eco
```

**RideService (CreateRideAsync):**
```
?? Searching for nearby drivers...
   Found X drivers within 3km
   Driver #3 data: status='Free', updatedAt='...'
   ✓ Driver #3 is AVAILABLE
?? Notifying X available drivers
```

**RideNotificationService:**
```
? Notified X drivers with ride #123 (fare: 5.50 AZN, distance: 2.34km)
```

---

## Redis CLI Inspection

If you have Redis CLI access:

```bash
# List all driver hashes
KEYS driver:*

# Get driver #3 data
HGETALL driver:3

# List all Eco drivers in geo index
GEOSEARCH geo:drivers:eco FROMMEMBER 3 BYRADIUS 100 km

# Get exact position of driver #3
GEOPOS geo:drivers:eco 3
```

---

## Quick Checklist

- [ ] Drivers are online and connected to SignalR
- [ ] Drivers sending `UpdateLocation` via `/hubs/driver`
- [ ] `GET /api/debug/driver-matching/drivers/0` returns drivers
- [ ] Driver hash contains `status="Free"` and recent `updatedAt`
- [ ] Geo-spatial index contains drivers (`geoIndices` not empty)
- [ ] Test match query returns `matched.count > 0`
- [ ] Rider can create ride
- [ ] RideService logs show matched drivers
- [ ] Drivers receive `NewRideRequest` notification
- [ ] RideNotificationService logs show notification sent

---

## Still Not Working?

1. Enable DEBUG logging in appsettings.json:
```json
{
  "Logging": {
    "LogLevel": {
      "TurboTaxi": "Debug",
      "Microsoft": "Warning"
    }
  }
}
```

2. Restart server and repeat tests

3. Share these logs:
   - DriverLocationService update logs
   - RideService.CreateRideAsync matching logs
   - RideNotificationService notification logs
   - `/api/debug/driver-matching/match` response

4. Share output from:
   - `GET /api/debug/driver-matching/drivers/0`
   - `GET /api/debug/driver-matching/driver/{problemDriver}`
   - `POST /api/debug/driver-matching/match` with actual rider location

