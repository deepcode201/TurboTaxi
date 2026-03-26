# 🚀 React Native Developer - Quick Start Guide

## What You Need to Know (30 seconds version)

### New Behavior
When a driver declines a ride:
- **Old:** Ride cancelled ❌
- **New:** Ride automatically goes to another driver ✅

### What Your App Needs to Do
Listen for one event and handle it:

```javascript
connection.on('RideStatusUpdated', (rideId, status, payload) => {
  if (payload.status === 'Pending' && payload.message.includes('Searching')) {
    // Show loading: "Finding another driver..."
  } else if (payload.status === 'Accepted') {
    // Show new driver info
  }
});
```

---

## 5-Minute Implementation

### Step 1: Setup Hook (Copy-Paste This)

```javascript
// hooks/useRideHub.js
import * as signalR from '@microsoft/signalr';
import { useEffect, useRef } from 'react';

export const useRideHub = (userId) => {
  const connectionRef = useRef(null);

  useEffect(() => {
    if (!userId) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('http://your-backend.com:5000/userhub', {
        withCredentials: true,
      })
      .withAutomaticReconnect()
      .build();

    connection.start()
      .then(() => {
        console.log('✅ Connected');
        connection.invoke('SubscribeUser', userId.toString());
      });

    // Main event handler
    connection.on('RideStatusUpdated', (rideId, status, payload) => {
      console.log('Ride Update:', payload.status, payload.message);
      
      // Handle reassignment
      if (payload.status === 'Pending' && payload.message.includes('Searching')) {
        handleReassignment(payload);
      } else if (payload.status === 'Accepted') {
        handleNewDriver(payload);
      }
    });

    connectionRef.current = connection;
    return () => connection.stop();
  }, [userId]);

  return connectionRef.current;
};

// Handle driver reassignment
function handleReassignment(payload) {
  console.log('Driver declined, finding new driver...');
  showLoadingSpinner(true);
  showNotification('Finding another driver...');
}

// Handle new driver accepted
function handleNewDriver(payload) {
  console.log('New driver accepted:', payload);
  showLoadingSpinner(false);
  showNotification('Driver accepted your ride!');
  updateDriverUI(payload);
}

function showLoadingSpinner(show) {
  // Your implementation
}

function showNotification(message) {
  // Your implementation
}

function updateDriverUI(driver) {
  // Your implementation
}
```

### Step 2: Use Hook in Your Ride Screen

```javascript
import { useRideHub } from './hooks/useRideHub';

export function RideActiveScreen({ route }) {
  const { rideId, userId } = route.params;
  
  // This handles all real-time updates
  const connection = useRideHub(userId);

  return (
    <View style={styles.container}>
      {/* Your existing UI */}
    </View>
  );
}
```

Done! That's it. 🎉

---

## What Events You'll See

### Scenario 1: Normal Ride (No Decline)
```
✅ Ride Created
  └─ Message: "Searching for nearby drivers..."
  
✅ Driver Accepted
  └─ Message: "Driver accepted your ride!"
  └─ Driver Info: Name, Rating, Location
  
✅ Driver On Way
  └─ Message: "Driver is on the way"
  
✅ Ride Completed
  └─ Message: "Trip completed! Total: 25.50 AZN"
```

### Scenario 2: Driver Declines (NEW!)
```
✅ Driver Accepted
  └─ Status: "Accepted"
  
❌ Driver Declined
  └─ Status: "Pending" ← Changed back to Pending!
  └─ Message: "Driver not available. Searching for another driver..."
  └─ Action: Show loading spinner
  
✅ New Driver Accepted
  └─ Status: "Accepted"
  └─ New Driver Info: Different driver!
  └─ Action: Hide loading, show new driver
```

---

## Copy-Paste Notification Handler

```javascript
// Use this function to handle all ride notifications
function handleRideNotification(payload) {
  const { status, message, finalPrice, rideId } = payload;

  switch (status) {
    case 'Pending':
      if (message.includes('Searching')) {
        // Driver declined, showing reassignment UI
        showUI('REASSIGNING', message);
      }
      break;

    case 'Accepted':
      // Driver accepted (new or initial)
      showUI('DRIVER_ACCEPTED', message);
      updateDriverInfo(payload);
      break;

    case 'OnWay':
      // Driver is coming
      showUI('DRIVER_ONWAY', message);
      break;

    case 'Completed':
      // Ride finished
      showUI('COMPLETED', message);
      showPrice(finalPrice);
      // Navigate to completed screen
      navigation.navigate('RideCompleted', { 
        rideId, 
        finalPrice 
      });
      break;

    case 'Canceled':
      // Ride was canceled
      showUI('CANCELED', message);
      navigation.navigate('Home');
      break;

    default:
      console.warn('Unknown status:', status);
  }
}

// Usage in event handler
connection.on('RideStatusUpdated', (rideId, status, payload) => {
  handleRideNotification(payload);
});
```

---

## Common Issues & Fixes

### Issue: "Not receiving reassignment notification"

**Fix:** Make sure subscription happens FIRST

```javascript
// ❌ WRONG
connection.on('RideStatusUpdated', ...); // Set listener first
connection.start(); // Then connect
connection.invoke('SubscribeUser', userId); // Then subscribe

// ✅ RIGHT
connection.start(); // Connect first
connection.invoke('SubscribeUser', userId); // Subscribe immediately
connection.on('RideStatusUpdated', ...); // Then listen
```

### Issue: "Showing old driver info when new driver accepted"

**Fix:** Update driver info when status changes

```javascript
// ❌ WRONG - Same handler for old and new driver
if (payload.status === 'Accepted') {
  showDriverInfo(payload); // Might be old driver still
}

// ✅ RIGHT - Always refresh driver info
connection.on('RideStatusUpdated', (rideId, status, payload) => {
  if (payload.status === 'Accepted') {
    // Clear old data first
    clearDriverInfo();
    // Show new driver
    showNewDriver(payload.driverName, payload.driverRating);
  }
});
```

### Issue: "Spinner shows but never goes away"

**Fix:** Hide spinner when new driver accepts

```javascript
// ✅ Correct flow
if (message.includes('Searching')) {
  showLoadingSpinner(true);
} else if (payload.status === 'Accepted') {
  showLoadingSpinner(false); // Hide here!
  showNewDriver(payload);
}
```

---

## Testing Checklist

- [ ] Backend running at http://your-backend.com:5000
- [ ] SignalR libraries installed
- [ ] useRideHub hook created
- [ ] Used hook in RideActiveScreen
- [ ] Can see "Searching for another driver..." message
- [ ] Can see new driver info when accepted
- [ ] No console errors

---

## Payload Reference

When you receive `RideStatusUpdated` event:

```javascript
{
  rideId: 123,
  status: 'Pending' | 'Accepted' | 'OnWay' | 'Completed' | 'Canceled',
  message: 'Human readable message',
  finalPrice?: 25.50,
  clearMap?: true,
  driverId?: 5,
  driverName?: 'Ahmed',
  driverRating?: 4.8,
  driverPhone?: '+994501234567',
  vehicleType?: 'Eco',
  vehicleNumber?: 'AZ 77 ABC'
}
```

---

## Full Example Screen

```javascript
import React, { useState, useEffect } from 'react';
import { View, Text, TouchableOpacity, ActivityIndicator } from 'react-native';
import { useRideHub } from './hooks/useRideHub';

export function RideActiveScreen({ route, navigation }) {
  const { rideId, userId } = route.params;
  const connection = useRideHub(userId);

  const [status, setStatus] = useState('Pending');
  const [isSearching, setIsSearching] = useState(false);
  const [driver, setDriver] = useState(null);
  const [message, setMessage] = useState('Finding driver...');

  useEffect(() => {
    if (!connection) return;

    connection.on('RideStatusUpdated', (rideId, status, payload) => {
      setMessage(payload.message);
      setStatus(payload.status);

      if (payload.status === 'Pending' && payload.message.includes('Searching')) {
        setIsSearching(true);
        setDriver(null);
      } else if (payload.status === 'Accepted') {
        setIsSearching(false);
        setDriver({
          name: payload.driverName,
          rating: payload.driverRating,
        });
      } else if (payload.status === 'Completed') {
        navigation.navigate('RideCompleted', { 
          finalPrice: payload.finalPrice 
        });
      }
    });
  }, [connection]);

  return (
    <View style={{ flex: 1, padding: 20, backgroundColor: '#f5f5f5' }}>
      <Text style={{ fontSize: 18, fontWeight: 'bold' }}>Ride #{rideId}</Text>
      <Text style={{ marginTop: 10, fontSize: 14 }}>{message}</Text>

      {isSearching ? (
        <View style={{ marginTop: 20, alignItems: 'center' }}>
          <ActivityIndicator size="large" color="#007bff" />
          <Text style={{ marginTop: 10 }}>Finding another driver...</Text>
        </View>
      ) : driver ? (
        <View style={{ marginTop: 20, padding: 15, backgroundColor: 'white', borderRadius: 8 }}>
          <Text style={{ fontSize: 16, fontWeight: 'bold' }}>{driver.name}</Text>
          <Text style={{ marginTop: 5 }}>⭐ {driver.rating}</Text>
        </View>
      ) : null}

      <TouchableOpacity
        style={{
          marginTop: 30,
          paddingVertical: 12,
          backgroundColor: '#dc3545',
          borderRadius: 8,
          alignItems: 'center',
        }}
      >
        <Text style={{ color: 'white', fontWeight: 'bold' }}>Cancel Ride</Text>
      </TouchableOpacity>
    </View>
  );
}
```

That's it! Copy this code, update the backend URL, and you're done. 🎉

---

## Need Help?

**Check these files for more details:**
1. `REACT_NATIVE_REASSIGNMENT_IMPLEMENTATION.md` - Full implementation guide
2. `DRIVER_DECLINE_COMPLETE_SUMMARY.md` - Complete overview
3. `DRIVER_DECLINE_CODE_REFERENCE.md` - Backend code details

**Questions?** Ask your backend team - they've already implemented this on the server side!
